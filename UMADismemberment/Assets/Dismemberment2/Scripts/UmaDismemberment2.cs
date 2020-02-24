using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UMA.CharacterSystem;

using System.Collections.Specialized;

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

        List<int> outerTris;
        List<int> innerTris;
        List<int> edges;

        private List<int> tris;

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
            //int[] computedMask = new int[smr.sharedMesh.vertexCount];
            bool[] computedMask = new bool[smr.sharedMesh.vertexCount];
            for (int i = 0; i < computedMask.Length; i++)
            {
                //computedMask[i] = (int)masks[i].x;

                //only using X for now, we could part another 32bits in the Y channel.
                computedMask[i] = (((int)masks[i].x & bitMask) != 0);

                /*byte[] bytes = BitConverter.GetBytes(masks[i].x);
                computedMask[i] = BitConverter.ToInt32(bytes, 0);
                if (computedMask[i] > 0)
                {
                    BitVector32 bv = new BitVector32(computedMask[i]);
                    Debug.Log("Found uv2 data, uv2: " + masks[i].x + " mask: " + computedMask[i] + " bits: " + bv);
                }*/
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

            for (int subMeshIndex = 0; subMeshIndex < smr.sharedMesh.subMeshCount; subMeshIndex++)
            {
                tris.Clear();
                innerMesh.GetTriangles(tris, subMeshIndex);
                innerTris.Clear();
                outerTris.Clear();

                for (int i = 0; i < tris.Count; i += 3)
                {
                    bool vert1 = computedMask[tris[i]];//(computedMask[tris[i]] & bitMask) != 0;
                    bool vert2 = computedMask[tris[i + 1]];//(computedMask[tris[i+1]] & bitMask) != 0;
                    bool vert3 = computedMask[tris[i + 2]];//(computedMask[tris[i+2]] & bitMask) != 0;

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

            if (edges.Count != 0)
            {
                CapMesh(newSmr.sharedMesh, edges, false);
                CapMesh(smr.sharedMesh, edges, true);

                //Copy over materials and add the chopFill materials
                int matCount = smr.sharedMaterials.Length;
                Material[] materials = new Material[matCount + 1];
                for (int i = 0; i < matCount; i++) { materials[i] = smr.sharedMaterials[i]; }
                materials[matCount] = sliceFillInstance;
                newSmr.sharedMaterials = materials;
                smr.sharedMaterials = materials;
            }
            else
            {
                //Debug.LogError("Edge count is zero!");
                newSmr.sharedMaterials = smr.sharedMaterials;
            }

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

        private void CapMesh(Mesh mesh, List<int> edges, bool facing = true)
        {
            if (mesh == null) return;
            if (edges.Count < 2) return;

            int[] capTris = new int[(edges.Count - 1) * 3];

            // generate fan of polys to cap the edges
            for (int a = 0; a < edges.Count - 1; a += 2)
            {
                capTris[a * 3 + 0] = edges[0];
                capTris[a * 3 + 1] = facing ? edges[a] : edges[a + 1];
                capTris[a * 3 + 2] = facing ? edges[a + 1] : edges[a];
            }

            int subMeshCount = mesh.subMeshCount;
            mesh.subMeshCount = subMeshCount + 1;
            mesh.SetTriangles(capTris, subMeshCount);
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
