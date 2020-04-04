using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace UMA.Dismemberment
{
    public class BitmaskUpdateWindow : EditorWindow
    {
        enum UVChannel
        {
            uv,
            uv2,
            uv3,
            uv4,
        }

        UVChannel channel = UVChannel.uv2;
        HumanBodyBones boneMask;

        List<SlotDataAsset> slotDataAssets = new List<SlotDataAsset>();

        Rect dropRect;

        [MenuItem("UMA/Bitmask Update Window")]
        static void Init()
        {
            // Get existing open window or if none, make a new one:
            BitmaskUpdateWindow window = (BitmaskUpdateWindow)EditorWindow.GetWindow(typeof(BitmaskUpdateWindow));
            window.Show();
        }

        void OnGUI()
        {
            channel = (UVChannel)EditorGUILayout.EnumPopup("UV Channel", channel);
            boneMask = (HumanBodyBones)EditorGUILayout.EnumPopup("Bone Bitmask", boneMask);

            int bitMask = (1 << (int)boneMask);

            if (GUILayout.Button("Apply To All SlotDataAssets", GUILayout.MinHeight(40)))
            {
                if (EditorUtility.DisplayDialog("Warning", "This will apply " + boneMask.ToString() + " to " + channel.ToString() + "\nThis will overwrite data.\nAre you sure?", "Yes", "Cancel"))
                {
                    ApplyMaskToAll(slotDataAssets, channel, bitMask);
                }
            }

            if (GUILayout.Button("Clear Mask From All SlotDataAssets", GUILayout.MinHeight(40)))
            {
                if (EditorUtility.DisplayDialog("Warning", "This will overwrite data. Are you sure?", "Yes", "Cancel"))
                {
                    ClearMaskFromAll(slotDataAssets, channel);
                }
            }

            dropRect = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drag and Drop SlotDataAssets Here");
            if (dropRect.Contains(Event.current.mousePosition))
            {
                DoDragAndDrop();
            }

            for(int i = slotDataAssets.Count-1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(slotDataAssets[i].name);
                if(GUILayout.Button("X", GUILayout.MaxWidth(20)))
                {
                    slotDataAssets.RemoveAt(i);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        void DoDragAndDrop()
        {
            if (Event.current.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                Event.current.Use();
            }
            else if (Event.current.type == EventType.DragPerform)
            {
                for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
                {
                    SlotDataAsset slot = DragAndDrop.objectReferences[i] as SlotDataAsset;
                    if(slot != null)
                    {
                        if(!slotDataAssets.Contains(slot))
                        {
                            slotDataAssets.Add(slot);
                        }
                    }
                }
                Event.current.Use();
            }
        }

        void ClearMaskFromAll(List<SlotDataAsset> slots, UVChannel channel)
        {
            if (slots == null)
                return;

            for (int slotsIndex = 0; slotsIndex < slots.Count; slotsIndex++)
            {
                switch (channel)
                {
                    case UVChannel.uv:
                        slots[slotsIndex].meshData.uv = new Vector2[0];
                        break;
                    case UVChannel.uv2:
                        slots[slotsIndex].meshData.uv2 = new Vector2[0];
                        break;
                    case UVChannel.uv3:
                        slots[slotsIndex].meshData.uv3 = new Vector2[0];
                        break;
                    case UVChannel.uv4:
                        slots[slotsIndex].meshData.uv4 = new Vector2[0];
                        break;
                    default:
                        slots[slotsIndex].meshData.uv2 = new Vector2[0];
                        break;
                }

                EditorUtility.SetDirty(slots[slotsIndex]);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Complete....");
        }

        void ApplyMaskToAll(List<SlotDataAsset> slots, UVChannel channel, int bitmask)
        {
            if (slots == null)
                return;
            
            for (int slotsIndex = 0; slotsIndex < slots.Count; slotsIndex++)
            {
                EditorUtility.DisplayProgressBar("Applying Data", slots[slotsIndex].slotName, (slotsIndex / slots.Count));
                switch (channel)
                {
                    case UVChannel.uv:
                        if(slots[slotsIndex].meshData.uv == null)
                        {
                            slots[slotsIndex].meshData.uv = new Vector2[slots[slotsIndex].meshData.vertexCount];
                        }
                        if (slots[slotsIndex].meshData.uv.Length == 0)
                        {
                            slots[slotsIndex].meshData.uv = new Vector2[slots[slotsIndex].meshData.vertexCount];
                        }
                        for (int i = 0; i < slots[slotsIndex].meshData.uv.Length; i++)
                        {
                            slots[slotsIndex].meshData.uv[i].x = bitmask;
                        }
                        break;
                    case UVChannel.uv2:
                        if (slots[slotsIndex].meshData.uv2 == null)
                        {
                            slots[slotsIndex].meshData.uv2 = new Vector2[slots[slotsIndex].meshData.vertexCount];
                        }
                        if (slots[slotsIndex].meshData.uv2.Length == 0)
                        {
                            slots[slotsIndex].meshData.uv2 = new Vector2[slots[slotsIndex].meshData.vertexCount];
                        }
                        for (int i = 0; i < slots[slotsIndex].meshData.uv2.Length; i++)
                        {
                            slots[slotsIndex].meshData.uv2[i].x = bitmask;
                        }
                        break;
                    case UVChannel.uv3:
                        if (slots[slotsIndex].meshData.uv3 == null)
                        {
                            slots[slotsIndex].meshData.uv3 = new Vector2[slots[slotsIndex].meshData.vertexCount];
                        }
                        if(slots[slotsIndex].meshData.uv3.Length == 0)
                        {
                            slots[slotsIndex].meshData.uv3 = new Vector2[slots[slotsIndex].meshData.vertexCount];
                        }
                        for (int i = 0; i < slots[slotsIndex].meshData.uv3.Length; i++)
                        {
                            slots[slotsIndex].meshData.uv3[i].x = bitmask;
                        }
                        break;
                    case UVChannel.uv4:
                        if (slots[slotsIndex].meshData.uv4 == null)
                        {
                            slots[slotsIndex].meshData.uv4 = new Vector2[slots[slotsIndex].meshData.vertexCount];
                        }
                        if (slots[slotsIndex].meshData.uv4.Length == 0)
                        {
                            slots[slotsIndex].meshData.uv4 = new Vector2[slots[slotsIndex].meshData.vertexCount];
                        }
                        for (int i = 0; i < slots[slotsIndex].meshData.uv4.Length; i++)
                        {
                            slots[slotsIndex].meshData.uv4[i].x = bitmask;
                        }
                        break;
                    default:
                        if (slots[slotsIndex].meshData.uv2 == null)
                        {
                            slots[slotsIndex].meshData.uv2 = new Vector2[slots[slotsIndex].meshData.vertexCount];
                        }
                        if (slots[slotsIndex].meshData.uv2.Length == 0)
                        {
                            slots[slotsIndex].meshData.uv2 = new Vector2[slots[slotsIndex].meshData.vertexCount];
                        }
                        for (int i = 0; i < slots[slotsIndex].meshData.uv2.Length; i++)
                        {
                            slots[slotsIndex].meshData.uv2[i].x = bitmask;
                        }
                        break;
                }

                EditorUtility.SetDirty(slots[slotsIndex]);
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            Debug.Log("Complete....");
        }
    }
}
