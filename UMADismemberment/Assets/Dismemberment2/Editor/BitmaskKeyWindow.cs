using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UMA.Dismemberment2
{
    public class BitmaskKeyWindow : EditorWindow
    {
        int numHumanBodyBones = 23;

        static string[] humanBodyBoneNames;

        [MenuItem("UMA/Bitmask Key Window")]
        static void Init()
        {
            // Get existing open window or if none, make a new one:
            BitmaskKeyWindow window = (BitmaskKeyWindow)EditorWindow.GetWindow(typeof(BitmaskKeyWindow));
            window.Show();

            humanBodyBoneNames = System.Enum.GetNames(typeof(HumanBodyBones));
        }

        private void OnGUI()
        {
            if(humanBodyBoneNames == null)
            {
                humanBodyBoneNames = System.Enum.GetNames(typeof(HumanBodyBones));
            }

            for(int i = 0; i < numHumanBodyBones; i++)
            {
                int setBitMask = (1 << i);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(humanBodyBoneNames[i]);
                EditorGUILayout.LabelField(setBitMask.ToString());

                byte[] bytes = new byte[4];
                int bone = i;
                if(bone <= 7)
                {
                    bytes[0] = (byte)(bone * 32 + 16);
                }
                if(bone > 7 && bone <= 15)
                {
                    bytes[1] = (byte)((bone - 8) * 32 + 16);
                }
                if(bone > 15 && bone <= 23)
                {
                    bytes[2] = (byte)((bone - 16) * 32 + 16);
                }

                Color32 color = new Color32(bytes[0], bytes[1], bytes[2], Byte.MaxValue /*bytes[3]*/);
                EditorGUILayout.ColorField(GUIContent.none, color, false, false, false);

                EditorGUILayout.EndHorizontal();
            }
        }

    }
}
