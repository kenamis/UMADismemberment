using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UMA.Dismemberment2
{
    [CustomEditor(typeof(UVSlotPainter))]
    public class UVSlotPainterEditor : Editor
    {
        SerializedProperty slotDataAsset;
        SerializedProperty selectedVerts;

        float size = 0.05f;
        Vector3[] vertices;
        bool[] selection;

        enum UVChannel
        {
            uv,
            uv2,
            uv3,
            uv4,
        }
        UVChannel uvChannel = UVChannel.uv2;
        HumanBodyBones bone;

        private void OnEnable()
        {
            slotDataAsset = serializedObject.FindProperty("slotDataAsset");
            selectedVerts = serializedObject.FindProperty("selectedVerts");

            SlotDataAsset slotData = (target as UVSlotPainter).slotDataAsset;
            if(slotData != null)
            {
                vertices = slotData.meshData.vertices;
                selectedVerts.arraySize = vertices.Length;
                selection = new bool[vertices.Length];
                for(int i = 0; i < vertices.Length; i++)
                {
                    selection[i] = selectedVerts.GetArrayElementAtIndex(i).boolValue;
                }
            }
        }

        public override void OnInspectorGUI()
        {
            SlotDataAsset slotData = (target as UVSlotPainter).slotDataAsset;
            //base.OnInspectorGUI();
            serializedObject.Update();

            size = EditorGUILayout.Slider("Handle Size", size, 0.01f, 0.1f);

            uvChannel = (UVChannel)EditorGUILayout.EnumPopup("Working Channel", uvChannel);
            bone = (HumanBodyBones)EditorGUILayout.EnumPopup("Working Mask", bone);
            int bitMask = (1 << (int)bone);

            if (GUILayout.Button("Set Selected Vertices to Mask"))
            {
                SetSelectedVerticesMask(slotData, bitMask);
            }

            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Set Selected Vertices as Edge"))
            {
                SetSelectedVerticesEdge(slotData, 1f);
            }
            if (GUILayout.Button("Clear Selected Vertices of Edge"))
            {
                SetSelectedVerticesEdge(slotData, 0f);
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Clear Selection"))
            {
                for(int i = 0; i < selectedVerts.arraySize; i++)
                {
                    selection[i] = false;
                    selectedVerts.GetArrayElementAtIndex(i).boolValue = false;
                }
            }

            if(GUILayout.Button("Select by Mask"))
            {
                if(slotData.meshData.uv2 != null)
                {
                    for (int i = 0; i < selectedVerts.arraySize; i++)
                    {
                        selection[i] = ((int)slotData.meshData.uv2[i].x & bitMask) != 0;
                        selectedVerts.GetArrayElementAtIndex(i).boolValue = selection[i];
                    }
                }
            }

            if(GUILayout.Button("Select by Edge"))
            {
                if (slotData.meshData.uv2 != null)
                {
                    for (int i = 0; i < selectedVerts.arraySize; i++)
                    {
                        selection[i] = slotData.meshData.uv2[i].y > 0;
                        selectedVerts.GetArrayElementAtIndex(i).boolValue = selection[i];
                    }
                }
            }

            if(GUILayout.Button("Select All"))
            {
                for (int i = 0; i < selectedVerts.arraySize; i++)
                {
                    selection[i] = true;
                    selectedVerts.GetArrayElementAtIndex(i).boolValue = true;
                }
            }

            if(GUILayout.Button("Save to SlotDataAsset"))
            {

            }

            serializedObject.ApplyModifiedProperties();
        }

        private void SetSelectedVerticesMask(SlotDataAsset slotData, int bitMask)
        {
            //temp
            if (uvChannel == UVChannel.uv2)
            {
                if (slotData.meshData.uv2 == null)
                {
                    slotData.meshData.uv2 = new Vector2[slotData.meshData.vertexCount];
                }

                for (int i = 0; i < selection.Length; i++)
                {
                    if (selection[i])
                    {
                        slotData.meshData.uv2[i].x = bitMask;
                    }
                }
            }

            Debug.Log("Complete....");
        }

        private void SetSelectedVerticesEdge(SlotDataAsset slotData, float edge)
        {
            //temp
            if (uvChannel == UVChannel.uv2)
            {
                if (slotData.meshData.uv2 == null)
                {
                    slotData.meshData.uv2 = new Vector2[slotData.meshData.vertexCount];
                }

                for (int i = 0; i < selection.Length; i++)
                {
                    if (selection[i])
                    {
                        slotData.meshData.uv2[i].y = edge;
                    }
                }
            }

            Debug.Log("Complete....");
        }

        protected virtual void OnSceneGUI()
        {
            if (vertices == null)
                return;

            Transform mat = (target as UVSlotPainter).transform;

            Handles.color = Color.red;
            for(int i = 0; i < vertices.Length; i++)
            {
                Vector3 point = mat.TransformPoint(vertices[i]);
                if(selection[i])
                {
                    Handles.color = Color.red;
                }
                else
                {
                    Handles.color = Color.black;
                }

                if(Handles.Button(point, Quaternion.identity, HandleUtility.GetHandleSize(point) * size, HandleUtility.GetHandleSize(point) * size, Handles.DotHandleCap ))
                {
                    selection[i] = !selection[i];
                    selectedVerts.GetArrayElementAtIndex(i).boolValue = selection[i]; //update serialized backer.
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
