using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UMA.CharacterSystem;

/// Tutorial used and inspired by http://log.idlecreations.com/2014/04/chopping-up-rag-dolls-in-unity.html?m=1
/// Written by Kenamis
/// Many optimizations thanks to Ecurtz
/// 
namespace UMA.Dismemberment
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
    public class UmaDismemberment : MonoBehaviour
    {
        [System.Serializable]
        public struct DismemberedInfo
        {
            public Transform root;
            public Transform targetBone;
        }

        [System.Serializable]
        public struct BoneInfo
        {
            public HumanBodyBones humanBone;
            [Range(0.01f, 1f)]
            public float threshold;
        }

        [Tooltip("Whether to invoke the Dismembered event or not.")]
        public bool useEvents = false;

        [Tooltip("The material to be used to cap the sliced area.")]
        public Material sliceFill;
        private Material sliceFillInstance;

        [Range(0.01f, 1f)]
        [Tooltip("The total weight threshold to include a vertex in the slice or not.")]
        public float globalThreshold = 0.01f;

        [Tooltip("If so, only allow these bones to be slice, otherwise any bone can be sliced.")]
        public bool useSliceable = true;
        [Tooltip("The human bones (and all its children) that are sliceable.")]
        public List<BoneInfo> sliceableHumanBones = new List<BoneInfo>();

        public HashSet<Transform> hasSplit;
        public Dismembered DismemberedEvent;

        DynamicCharacterAvatar avatar;
        SkinnedMeshRenderer smr; //Will need to deal with multiple renderers eventually
        Animator animator;

        List<int> outerTris;
        List<int> innerTris;
        List<int> edges;

        private List<int> tris;
        private List<BoneWeight> weights;

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
            weights = new List<BoneWeight>(10000);
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
        
        private bool[] GenerateBoneNumbers(Transform bone)
        {
            if (bone == null)
                return null;
            //Profiler.BeginSample("GenerateBoneNumbers");
            Transform[] smrBones = smr.bones;
            bool[] bones = new bool[smrBones.Length];

            foreach (Transform t in bone.GetComponentsInChildren<Transform>())
            {
                for (int i = 0; i < smrBones.Length; i++)
                {
                    if (smrBones[i] == t) { bones[i] = true; }
                }
            }
            //Profiler.EndSample();
            return bones;
        }

        /// <summary>
        /// Slice by Human Bone.
        /// </summary>
        /// <param name="humanBone">The Human Bone to slice.</param>
        /// <param name="info">Struct containing slice info.</param>
        /// <param name="useGlobalThreshold">Turn off global threshold or individual bone threshold.</param>
        public void Slice(HumanBodyBones humanBone, out DismemberedInfo info, bool useGlobalThreshold = false)
        {
            info = new DismemberedInfo();

            Transform bone;
            if (!ValidateHumanBone(humanBone, out bone))
                return;

            int index = -1;
            if (useSliceable)
            {
                index = ContainsBone(humanBone);
                if (index == -1)
                    return;
            }

            if (hasSplit.Contains(bone))
                return;

            if (!useSliceable || useGlobalThreshold)
                SliceInternal(bone, globalThreshold, ref info);
            else
                SliceInternal(bone, sliceableHumanBones[index].threshold, ref info);
        }

        /// <summary>
        /// Slice by Human Bone.
        /// </summary>
        /// <param name="humanBone">The Human Bone to slice.</param>
        /// <param name="hasNotSplit"></param>
        /// <param name="info">Struct containing slice info.</param>
        /// <param name="useGlobalThreshold">Turn off global threshold or individual bone threshold.</param>
        public void Slice(HumanBodyBones humanBone, bool hasNotSplit, out DismemberedInfo info, bool useGlobalThreshold = false)
        {
            info = new DismemberedInfo();

            Transform bone;
            if (!ValidateHumanBone(humanBone, out bone))
                return;

            int index = -1;
            if (useSliceable)
            {
                index = ContainsBone(humanBone);
                if ( index == -1 )
                    return;
            }

            if (hasNotSplit)
            {
                if (hasSplit.Contains(bone))
                    return;
            }

            if (!useSliceable || useGlobalThreshold)
                SliceInternal(bone, globalThreshold, ref info);
            else
                SliceInternal(bone, sliceableHumanBones[index].threshold, ref info);
        }

        /// <summary>
        /// Slice by bone transform.
        /// </summary>
        /// <param name="bone">The bone transform to slice.</param>
        /// <param name="info">Struct containing slice info.</param>
        public void Slice(Transform bone, out DismemberedInfo info)
        {
            info = new DismemberedInfo();
            SliceInternal(bone, globalThreshold, ref info);
        }

        /// <summary>
        /// Slice by bone transform.
        /// </summary>
        /// <param name="bone">The bone transform to slice.</param>
        /// <param name="threshold">Threshold to determine whether a vertex is sliced or not.</param>
        /// <param name="info">Struct containing slice info.</param>
        public void Slice(Transform bone, float threshold, out DismemberedInfo info)
        {
            info = new DismemberedInfo();
            SliceInternal(bone, threshold, ref info);
        }

        private void SliceInternal(Transform bone, float threshold, ref DismemberedInfo info)
        {
            if (bone == null)
            {
                if (Debug.isDebugBuild)
                    Debug.LogError("Bone is null!");
                return;
            }
            //Profiler.BeginSample("Slice");
            edges.Clear();
            weights.Clear();
            smr.sharedMesh.GetBoneWeights(weights);

            Transform[] smrBones = smr.bones;
            bool[] boneMask = GenerateBoneNumbers(bone);
            bool[] computedWeights = new bool[weights.Count];
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

            //precompute the weights so we aren't wasting computation on the same vertex.
            for (int i = 0; i < computedWeights.Length; i++)
            {
                computedWeights[i] = IsPartOf(weights[i], boneMask, threshold);
            }

            for (int subMeshIndex = 0; subMeshIndex < smr.sharedMesh.subMeshCount; subMeshIndex++)
            {
                tris.Clear();
                innerMesh.GetTriangles(tris, subMeshIndex);
                innerTris.Clear();
                outerTris.Clear();

                for (int i = 0; i < tris.Count; i += 3)
                {
                    bool vert1 = computedWeights[tris[i]];
                    bool vert2 = computedWeights[tris[i + 1]];
                    bool vert3 = computedWeights[tris[i + 2]];

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

            CapMesh(newSmr.sharedMesh, edges, false);
            CapMesh(smr.sharedMesh, edges, true);

            //Copy over materials and add the chopFill materials
            int matCount = smr.sharedMaterials.Length;
            Material[] materials = new Material[matCount + 1];
            for (int i = 0; i < matCount; i++) { materials[i] = smr.sharedMaterials[i]; }
            materials[matCount] = sliceFillInstance;
            newSmr.sharedMaterials = materials;
            smr.sharedMaterials = materials;

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

        private GameObject CreateBones(GameObject rootToClone, GameObject parent, Transform[] bonesToKeep, int boneCount, Transform targetBoneName, out Transform[] newBones, out Transform targetBone )
        {
            targetBone = null;
            newBones = new Transform[boneCount];
            return CreateBonesRecursive(rootToClone, parent, bonesToKeep, targetBoneName, newBones, ref targetBone);
        }

        private GameObject CreateBonesRecursive( GameObject rootToClone, GameObject parent, Transform[] bonesToKeep, Transform targetBoneName, Transform[] newBones, ref Transform targetBone )
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

            for (int i = 0; i <  rootToClone.transform.childCount; i++)
            {
                CreateBonesRecursive(rootToClone.transform.GetChild(i).gameObject, newObj, bonesToKeep, targetBoneName, newBones, ref targetBone);
            }

            return newObj;
        }

        private static bool IsPartOf(BoneWeight b, bool[] indices, float threshold)
        {
            float weight = 0;

            if (indices[b.boneIndex0]) weight += b.weight0;
            if (indices[b.boneIndex1]) weight += b.weight1;
            if (indices[b.boneIndex2]) weight += b.weight2;
            if (indices[b.boneIndex3]) weight += b.weight3;

            return (weight > threshold);
        }

        private void CapMesh(Mesh mesh, List<int> edges, bool facing = true )
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

        private int ContainsBone(HumanBodyBones humanBone)
        {
            for(int i = 0; i < sliceableHumanBones.Count; i++)
            {
                if (sliceableHumanBones[i].humanBone == humanBone)
                    return i;
            }
            return -1;
        }
    }
}
