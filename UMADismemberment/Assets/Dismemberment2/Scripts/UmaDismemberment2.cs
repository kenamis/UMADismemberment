using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UMA.CharacterSystem;
using System.Collections.Specialized;

#if UNITY_EDITOR
using UnityEngine.Profiling;
#endif

/// Tutorial used and inspired by http://log.idlecreations.com/2014/04/chopping-up-rag-dolls-in-unity.html?m=1
/// Written by Kenamis
/// Many optimizations thanks to Ecurtz
/// 

namespace UMA.Dismemberment2
{
    [System.Serializable]
    public class Dismembered : UnityEvent<Transform, Transform>
    {
    }
    /// <summary>
    /// Component to allow dismembering a DynamicCharacterAvatar
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DynamicCharacterAvatar))]
    public class UmaDismemberment2 : MonoBehaviour
    {
        [System.Serializable]
        public struct DismemberedInfo
        {
            public Transform root;
            public Transform targetBone;
        }

        [Tooltip("Whether to invoke the Dismembered event or not.")]
        public bool useEvents = false;

        [Tooltip("The material to be used to cap the sliced area.")]
        public Material sliceFill;
        private Material sliceFillInstance;

        public DismembermentHierarchyAsset hierarchyAsset;

        [Tooltip("If so, only allow these bones to be slice, otherwise any bone can be sliced.")]
        public bool useSliceable = true;
        [Tooltip("The human bones (and all its children) that are sliceable.")]
        public List<HumanBodyBones> sliceableHumanBones = new List<HumanBodyBones>();

        public HashSet<Transform> hasSplit;
        public Dismembered DismemberedEvent;

        DynamicCharacterAvatar avatar;
        SkinnedMeshRenderer smr; //Will need to deal with multiple renderers eventually
        Animator animator;

        Dictionary<int, SkinnedMeshRenderer> capMeshes = new Dictionary<int, SkinnedMeshRenderer>();

        List<int> outerTris;
        List<int> innerTris;
        List<int> edges;

        private List<int> tris;

        static List<Vector3> verticesBuffer = new List<Vector3>(20); //reuse list to reduce garbage in cap
        static List<Vector3> parentVerticesBuffer = new List<Vector3>();
        static List<int> trianglesBuffer = new List<int>(60);
        static List<Vector2> uvBuffer = new List<Vector2>(20);
        static List<Vector3> normalBuffer = new List<Vector3>(20);
        static List<BoneWeight> boneweightBuffer = new List<BoneWeight>(20);
        static List<BoneWeight> parentBoneweightBuffer = new List<BoneWeight>();

        // Use this for initialization
        void Start()
        {
            avatar = GetComponent<DynamicCharacterAvatar>();
            avatar.CharacterCreated.AddListener(CharacterCreated);
            avatar.CharacterUpdated.AddListener(CharacterUpdated);

            if (DismemberedEvent == null)
                DismemberedEvent = new Dismembered();

            hasSplit = new HashSet<Transform>();

            sliceFillInstance = new Material(sliceFill);

            outerTris = new List<int>(40000);
            innerTris = new List<int>(40000);
            edges = new List<int>(500);
            tris = new List<int>(8000);
        }

        void OnValidate()
        {
            //TODO Check for duplicate human bones in list
        }

        void CharacterCreated(UMAData umaData)
        {
            animator = GetComponent<Animator>();
        }

        void CharacterUpdated(UMAData umaData)
        {
            smr = umaData.GetRenderer(0);
        }

        /// <summary>
        /// Slice by Human Bone.
        /// </summary>
        /// <param name="humanBone">The Human Bone to slice.</param>
        /// <param name="info">Struct containing slice info.</param>
        /// <param name="useGlobalThreshold">Turn off global threshold or individual bone threshold.</param>
        public void Slice(HumanBodyBones humanBone, out DismemberedInfo info, int uvChannel = 2)
        {
            info = new DismemberedInfo();

            if (hierarchyAsset == null)
                return;

            Transform bone;
            if (!ValidateHumanBone(humanBone, out bone))
                return;

            if (useSliceable)
            {
                if(!sliceableHumanBones.Contains(humanBone))
                    return;
            }

            if (hasSplit.Contains(bone))
                return;
            
            int bitMask = 0;
            if (hierarchyAsset.sliceMasks.TryGetValue(humanBone, out bitMask))
            {
                SliceInternal(bone, bitMask, ref info, uvChannel);
            }
        }

        private void SliceInternal(Transform bone, int bitMask, ref DismemberedInfo info, int uvChannel = 2)
        {
            if (bone == null)
            {
                if (Debug.isDebugBuild)
                    Debug.LogError("Bone is null!");
                return;
            }
            //Profiler.BeginSample("Slice");
            edges.Clear();

            Transform[] smrBones = smr.bones;
            Vector2[] masks;
            switch(uvChannel)
            {
                case 1:
                    masks = smr.sharedMesh.uv;
                    break;
                case 2:
                    masks = smr.sharedMesh.uv2;
                    break;
                case 3:
                    masks = smr.sharedMesh.uv3;
                    break;
                case 4:
                    masks = smr.sharedMesh.uv4;
                    break;
                default:
                    masks = smr.sharedMesh.uv2;
                    break;
            }

            bool[] computedMask = new bool[smr.sharedMesh.vertexCount];
            for (int i = 0; i < computedMask.Length; i++)
            {
                //only using X for now, we could part another 32bits in the Y channel.
                computedMask[i] = (((int)masks[i].x & bitMask) != 0);
            }

            Mesh innerMesh = Instantiate<Mesh>(smr.sharedMesh);

            //Create new root GameObject and all it's children bones
            GameObject splitRootObj = new GameObject(bone.name, typeof(SkinnedMeshRenderer));
            splitRootObj.transform.position = gameObject.transform.position;
            splitRootObj.transform.rotation = gameObject.transform.rotation;
            splitRootObj.transform.localScale = gameObject.transform.localScale;

            GameObject root = gameObject.transform.Find("Root").gameObject;
            Transform[] newBones;
            Transform targetBone;
            CreateBones(root, splitRootObj, smrBones, smrBones.Length, bone, out newBones, out targetBone);

            //Create the new renderer to copy the split mesh to.
            SkinnedMeshRenderer newSmr = splitRootObj.GetComponent<SkinnedMeshRenderer>();
            newSmr.bones = newBones;
            newSmr.sharedMesh = innerMesh;
            newSmr.sharedMesh.name = bone.name;
            newSmr.sharedMaterials = smr.sharedMaterials;

            for (int subMeshIndex = 0; subMeshIndex < smr.sharedMesh.subMeshCount; subMeshIndex++)
            {
                tris.Clear();
                innerMesh.GetTriangles(tris, subMeshIndex);
                innerTris.Clear();
                outerTris.Clear();

                for (int i = 0; i < tris.Count; i += 3)
                {
                    bool vert1 = computedMask[tris[i]];
                    bool vert2 = computedMask[tris[i + 1]];
                    bool vert3 = computedMask[tris[i + 2]];

                    if (vert1 || vert2 || vert3)
                    {
                        if (vert1 && !vert2 && !vert3) { edges.Add(tris[i + 1]); edges.Add(tris[i + 2]); }
                        if (!vert1 && vert2 && !vert3) { edges.Add(tris[i + 2]); edges.Add(tris[i + 0]); }
                        if (!vert1 && !vert2 && vert3) { edges.Add(tris[i + 0]); edges.Add(tris[i + 1]); }

                        innerTris.Add(tris[i]);
                        innerTris.Add(tris[i + 1]);
                        innerTris.Add(tris[i + 2]);
                    }
                    else
                    {
                        outerTris.Add(tris[i]);
                        outerTris.Add(tris[i + 1]);
                        outerTris.Add(tris[i + 2]);
                    }
                }

                smr.sharedMesh.SetTriangles(outerTris, subMeshIndex);
                innerMesh.SetTriangles(innerTris, subMeshIndex);
            }

            GameObject capInner = new GameObject("Cap", typeof(SkinnedMeshRenderer));
            GameObject capOuter = new GameObject("Cap", typeof(SkinnedMeshRenderer));

            SkinnedMeshRenderer capInnerSmr = capInner.GetComponent<SkinnedMeshRenderer>();
            SkinnedMeshRenderer capOuterSmr = capOuter.GetComponent<SkinnedMeshRenderer>();

            //Reparent the cap mesh on the SMR to the new SMR
            List<int> removeList = new List<int>();
            foreach(KeyValuePair<int,SkinnedMeshRenderer> pair in capMeshes)
            {
                int pairMask = (pair.Key & bitMask);
                if (pairMask != 0 && pairMask != bitMask)
                {
                    pair.Value.transform.SetParent(newSmr.transform);
                    pair.Value.bones = newSmr.bones;
                    removeList.Add(pair.Key);
                }
            }
            foreach(int id in removeList)
            {
                capMeshes.Remove(id);
            }

            capMeshes.Add(bitMask, capOuterSmr);

            capInner.transform.SetParent(newSmr.transform);
            capOuter.transform.SetParent(smr.transform);
            
            capInnerSmr.sharedMesh = CapMesh(innerMesh, edges, false);
            capOuterSmr.sharedMesh = CapMesh(smr.sharedMesh, edges, true);
            capInnerSmr.bones = newSmr.bones;
            capOuterSmr.bones = smr.bones;
            capInnerSmr.materials = new Material[1];
            capInnerSmr.material = sliceFillInstance;
            capOuterSmr.materials = new Material[1];
            capOuterSmr.material = sliceFillInstance;

            hasSplit.Add(bone);

            //Fill out DismemberedInfo
            info.root = splitRootObj.transform;
            info.targetBone = targetBone;

            //Event callback
            if (useEvents)
            {
                DismemberedEvent.Invoke(splitRootObj.transform, targetBone);
            }
            //Profiler.EndSample();
        }

        private GameObject CreateBones(GameObject rootToClone, GameObject parent, Transform[] bonesToKeep, int boneCount, Transform targetBoneName, out Transform[] newBones, out Transform targetBone)
        {
            targetBone = null;
            newBones = new Transform[boneCount];
            return CreateBonesRecursive(rootToClone, parent, bonesToKeep, targetBoneName, newBones, ref targetBone);
        }

        private GameObject CreateBonesRecursive(GameObject rootToClone, GameObject parent, Transform[] bonesToKeep, Transform targetBoneName, Transform[] newBones, ref Transform targetBone)
        {
            GameObject newObj = new GameObject(rootToClone.name);
            newObj.transform.SetParent(parent.transform);
            newObj.transform.localPosition = rootToClone.transform.localPosition;
            newObj.transform.localRotation = rootToClone.transform.localRotation;
            newObj.transform.localScale = rootToClone.transform.localScale;

            if (rootToClone.transform == targetBoneName)
                targetBone = newObj.transform;

            //Check if we keep this bone transforms for the smr bone list.
            for (int i = 0; i < bonesToKeep.Length; i++)
            {
                if (newBones[i] != null)
                    continue;

                if (bonesToKeep[i] == rootToClone.transform)
                {
                    newBones[i] = newObj.transform;
                    //break; //Can't early out because sometimes there are duplicates in the bone list
                }
            }

            for (int i = 0; i < rootToClone.transform.childCount; i++)
            {
                CreateBonesRecursive(rootToClone.transform.GetChild(i).gameObject, newObj, bonesToKeep, targetBoneName, newBones, ref targetBone);
            }

            return newObj;
        }

        static Mesh CapMesh(Mesh parent, List<int> edges, bool facing = true)
        {
            //Profiler.BeginSample("CapMesh");
            if (edges.Count < 2) return null;

            verticesBuffer.Clear();
            uvBuffer.Clear();
            normalBuffer.Clear();
            boneweightBuffer.Clear();

            parent.GetVertices(parentVerticesBuffer);
            parent.GetBoneWeights(parentBoneweightBuffer);

            for(int i = 0; i < edges.Count; i++)
            {
                verticesBuffer.Add(parentVerticesBuffer[edges[i]]);
                uvBuffer.Add(Vector2.zero);
                normalBuffer.Add(Vector3.zero);
                boneweightBuffer.Add(parentBoneweightBuffer[edges[i]]);
            }

            trianglesBuffer.Clear();
            trianglesBuffer.AddRange( new int[(edges.Count - 1) * 3]);

            // calculate uv map limits
            Vector2 UVLimits_x = Vector2.zero;
            Vector2 UVLimits_y = Vector2.zero;
            Quaternion plane = CapOrientation(verticesBuffer, edges);
            for (int a = 0; a < edges.Count - 1; a += 2)
            {
                Vector3 v1 = plane * verticesBuffer[a];//(verticesBuffer[edges[a]]);
                if ((a == 0) || v1.x < UVLimits_x[0]) UVLimits_x[0] = v1.x;
                if ((a == 0) || v1.x > UVLimits_x[1]) UVLimits_x[1] = v1.x;
                if ((a == 0) || v1.y < UVLimits_y[0]) UVLimits_y[0] = v1.y;
                if ((a == 0) || v1.y > UVLimits_y[1]) UVLimits_y[1] = v1.y;
                Vector3 v2 = plane * verticesBuffer[a + 1];//(verticesBuffer[edges[a + 1]]);
                if ((a == 0) || v2.x < UVLimits_x[0]) UVLimits_x[0] = v2.x;
                if ((a == 0) || v2.x > UVLimits_x[1]) UVLimits_x[1] = v2.x;
                if ((a == 0) || v2.y < UVLimits_y[0]) UVLimits_y[0] = v2.y;
                if ((a == 0) || v2.y > UVLimits_y[1]) UVLimits_y[1] = v2.y;
            }

            // generate fan of polys to cap the edges
            for (int a = 0; a < edges.Count - 1; a += 2)
            {
                trianglesBuffer[a * 3 + 0] = 0;//;edges[0];
                trianglesBuffer[a * 3 + 1] = facing ? a : a + 1;//edges[a] : edges[a + 1];
                trianglesBuffer[a * 3 + 2] = facing ? a + 1 : a;//edges[a + 1] : edges[a];

                for (int i = 0; i < 3; i++)
                {
                    Vector3 v = plane  * (verticesBuffer[trianglesBuffer[a * 3 + i]]);
                    uvBuffer[trianglesBuffer[a * 3 + i]] = new Vector2((v.x - UVLimits_x[0]) / (UVLimits_x[1] - UVLimits_x[0]), (v.y - UVLimits_y[0]) / (UVLimits_y[1] - UVLimits_y[0]));
                    normalBuffer[trianglesBuffer[a * 3 + i]] = facing ? plane * Vector3.back : plane * Vector3.forward;
                }
            }

            Mesh m = new Mesh();
            m.name = "CapMesh";
            m.SetVertices(verticesBuffer);
            m.bindposes = parent.bindposes;
            m.boneWeights = boneweightBuffer.ToArray();
            m.SetTriangles(trianglesBuffer, 0);
            m.SetUVs(0, uvBuffer);
            m.SetNormals(normalBuffer);
            m.RecalculateNormals();

            //Profiler.EndSample();
            return m;
        }

        static Quaternion CapOrientation(Vector3[] verts, List<int> edges)
        {
            // rough guess as to the orientation of the vertices
            int third = Mathf.FloorToInt(edges.Count / 3);
            int twothird = Mathf.FloorToInt(edges.Count * 2 / 3);
            Vector3 v1 = verts[edges[0]];
            Vector3 v2 = verts[edges[third]];
            Vector3 v3 = verts[edges[twothird]];
            return Quaternion.LookRotation(Vector3.Cross(v1 - v2, v3 - v2));
        }

        static Quaternion CapOrientation(List<Vector3> verts, List<int> edges)
        {
            // rough guess as to the orientation of the vertices
            int third = Mathf.FloorToInt(edges.Count / 3);
            int twothird = Mathf.FloorToInt(edges.Count * 2 / 3);
            Vector3 v1 = verts[0];//verts[edges[0]];
            Vector3 v2 = verts[third];//verts[edges[third]];
            Vector3 v3 = verts[twothird];//verts[edges[twothird]];
            return Quaternion.LookRotation(Vector3.Cross(v1 - v2, v3 - v2));
        }

        private bool ValidateHumanBone(HumanBodyBones humanBone, out Transform bone)
        {
            bone = null;
            if (animator == null)
            {
                if (Debug.isDebugBuild)
                    Debug.LogError("No animator found!");

                return false;
            }

            if (!animator.isHuman)
            {
                if (Debug.isDebugBuild)
                    Debug.LogError("Avatar is not a humanoid rig!");

                return false;
            }
            bone = animator.GetBoneTransform(humanBone);
            return true;
        }
    }
}
