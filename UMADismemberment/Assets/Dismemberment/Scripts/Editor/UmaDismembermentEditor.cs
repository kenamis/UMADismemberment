using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UMA.CharacterSystem;
using UnityEditorInternal;

namespace UMA.Dismemberment
{
    [CustomEditor(typeof(UmaDismemberment))]
    public class UmaDismembermentEditor : Editor
    {
        SerializedProperty useEvents;
        SerializedProperty sliceFill;
        SerializedProperty globalThreshold;
        SerializedProperty useSliceable;
        SerializedProperty sliceableHumanBones;
        SerializedProperty dismemberedEvent;

        ReorderableList sliceableList;

        void OnEnable()
        {
            useEvents = serializedObject.FindProperty("useEvents");
            sliceFill = serializedObject.FindProperty("sliceFill");
            globalThreshold = serializedObject.FindProperty("globalThreshold");
            useSliceable = serializedObject.FindProperty("useSliceable");
            sliceableHumanBones = serializedObject.FindProperty("sliceableHumanBones");
            dismemberedEvent = serializedObject.FindProperty("DismemberedEvent");

            sliceableList = new ReorderableList(serializedObject, sliceableHumanBones, true, true, true, true);
            sliceableList.drawElementCallback = DrawElement;
            sliceableList.drawHeaderCallback = DrawHeader;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUILayout.Space(10);
            EditorGUILayout.PropertyField(useEvents);
            EditorGUILayout.PropertyField(sliceFill);
            EditorGUILayout.PropertyField(globalThreshold);

            GUILayout.Space(10);
            EditorGUILayout.PropertyField(useSliceable);
            EditorGUI.BeginDisabledGroup(!useSliceable.boolValue);
            //EditorGUILayout.PropertyField(sliceableHumanBones, true);
            sliceableList.DoLayoutList();
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(20);
            EditorGUI.BeginDisabledGroup(!useEvents.boolValue);
            EditorGUILayout.PropertyField(dismemberedEvent);
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader(Rect rect)
        {
            GUI.Label(rect, "Sliceable Human Bones");
        }

        private void DrawElement(Rect rect, int index, bool active, bool focused)
        {
            SerializedProperty element = sliceableHumanBones.GetArrayElementAtIndex(index);
            SerializedProperty bone = element.FindPropertyRelative("humanBone");
            SerializedProperty threshold = element.FindPropertyRelative("threshold");
            rect.y += EditorGUIUtility.standardVerticalSpacing;
            float width = EditorGUIUtility.currentViewWidth - 60f;

            EditorGUI.PropertyField(new Rect( rect.x, rect.y, width * 0.3f, EditorGUIUtility.singleLineHeight), bone, new GUIContent());
            EditorGUI.PropertyField(new Rect( rect.x + width * 0.3f + 10, rect.y, width * 0.7f - 10, EditorGUIUtility.singleLineHeight), threshold, new GUIContent());
        }
    }
}