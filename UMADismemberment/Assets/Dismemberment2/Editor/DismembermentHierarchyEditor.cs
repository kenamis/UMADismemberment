using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace UMA.Dismemberment
{
    [CustomEditor(typeof(DismembermentHierarchyAsset))]
    public class DismembermentHierarchyEditor : Editor
    {
        SerializedProperty maskNames;
        SerializedProperty bitMasks;
        //SerializedProperty boneNamesID;
        SerializedProperty humanBodyBoneIndexLimit;

        string[] boneNames;

        void OnEnable()
        {
            maskNames = serializedObject.FindProperty("maskNames");
            bitMasks = serializedObject.FindProperty("bitMasks");
            //boneNamesID = serializedObject.FindProperty("boneNamesID");
            humanBodyBoneIndexLimit = serializedObject.FindProperty("humanBodyBoneIndexLimit");

            /*boneNames = new string[boneNamesID.arraySize];
            for(int i = 0; i < boneNamesID.arraySize; i++)
            {
                boneNames[i] = Enum.GetName(typeof(HumanBodyBones), boneNamesID.GetArrayElementAtIndex(i).enumValueIndex);
            }*/

            boneNames = new string[humanBodyBoneIndexLimit.intValue];
            for(int i = 0; i < boneNames.Length; i++)
            {
                boneNames[i] = Enum.GetName(typeof(HumanBodyBones), i);
            }
        }

        public override void OnInspectorGUI()
        {
            //base.OnInspectorGUI();

            DismembermentHierarchyAsset asset = target as DismembermentHierarchyAsset;
            serializedObject.Update();

            //EditorGUILayout.PropertyField(boneNamesID, true);
            EditorGUILayout.PropertyField(humanBodyBoneIndexLimit);
            GUILayout.Space(20);

            int count = Mathf.Min(maskNames.arraySize, bitMasks.arraySize);
            for(int i = count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.PropertyField(maskNames.GetArrayElementAtIndex(i), new GUIContent());

                bitMasks.GetArrayElementAtIndex(i).intValue = EditorGUILayout.MaskField("", bitMasks.GetArrayElementAtIndex(i).intValue, boneNames);

                if (GUILayout.Button("X"))
                {
                    asset.sliceMasks.Remove((HumanBodyBones)maskNames.GetArrayElementAtIndex(i).enumValueIndex);
                }
                EditorGUILayout.EndHorizontal();

                if(!includesItself( (HumanBodyBones)maskNames.GetArrayElementAtIndex(i).enumValueIndex, bitMasks.GetArrayElementAtIndex(i).intValue))
                {
                    EditorGUILayout.HelpBox("This entry does not include itself.", MessageType.Warning);
                }


            }

            if (GUILayout.Button("Add New"))
            {
                HumanBodyBones newKey = 0;
                while( asset.sliceMasks.ContainsKey(newKey))
                {
                    newKey++;
                }

                asset.sliceMasks.Add(newKey, 0);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private bool includesItself(HumanBodyBones bone, int bitMask)
        {
            int boneMask = 1 << (int)bone;
            //Debug.Log(boneMask);
            return ((boneMask & bitMask) != 0);
        }
    }
}
