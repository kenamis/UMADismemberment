using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UMA.Dismemberment2
{

    [CreateAssetMenu(fileName = "DismembermentHierarchy", menuName = "UMA/Dismemberment/Hierarchy Asset")]
    public class DismembermentHierarchyAsset : ScriptableObject, ISerializationCallbackReceiver
    {
        public Dictionary<HumanBodyBones, int> sliceMasks = new Dictionary<HumanBodyBones, int>();

#if UNITY_EDITOR
        // for in the mask field display, max 32 for 32bit mask
#pragma warning disable 0414
        [SerializeField]
        private int humanBodyBoneIndexLimit = 23;
#pragma warning restore 0414
#endif

        [SerializeField]
        private List<HumanBodyBones> maskNames = new List<HumanBodyBones>();
        [SerializeField]
        private List<int> bitMasks = new List<int>();


        public void OnBeforeSerialize()
        {
            maskNames.Clear();
            bitMasks.Clear();
            
            foreach(var kvp in sliceMasks)
            {
                maskNames.Add(kvp.Key);
                bitMasks.Add(kvp.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            sliceMasks = new Dictionary<HumanBodyBones, int>();

            for (int i = 0; i != Mathf.Min(maskNames.Count, bitMasks.Count); i++)
            {
                //if (!sliceMasks.ContainsKey(maskNames[i]))
                //{
                    sliceMasks.Add(maskNames[i], bitMasks[i]);
                //}
            }
        }
    }
}
