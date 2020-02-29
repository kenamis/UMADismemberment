using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UMA.Dismemberment2
{
    [CustomEditor(typeof(UVSlotPainter))]
    public class UVSlotPainterEditor : Editor
    {
        public struct Edge : IEquatable<Edge>
        {
            public Vector3 v1;
            public int index1;
            public Vector3 v2;
            public int index2;

            public Edge(Vector3 v1, int index1, Vector3 v2, int index2)
            {
                if (v1.x < v2.x || (v1.x == v2.x && (v1.y < v2.y || (v1.y == v2.y && v1.z <= v2.z))))
                {
                    this.v1 = v1;
                    this.index1 = index1;
                    this.v2 = v2;
                    this.index2 = index2;
                }
                else
                {
                    this.v1 = v2;
                    this.index1 = index2;
                    this.v2 = v1;
                    this.index2 = index1;
                }
            }

            public bool Equals(Edge other)
            {
                if (v1 == other.v1 && v2 == other.v2)
                    return true;

                return false;
            }
        }

        Mesh meshData;
        SerializedProperty slotDataAsset;
        SerializedProperty selectedVerts;
        SerializedProperty selectionColor;
        SerializedProperty bitMaskColors;

        bool showMaskColors = false;

        float size = 0.05f;
        Vector3[] vertices;
        bool[] selection;

		Dictionary<int, List<int>> vertexLocations;

        enum UVChannel
        {
            uv,
            uv2,
            uv3,
            uv4,
        }
        UVChannel handleChannel = UVChannel.uv2;
        UVChannel setChannel = UVChannel.uv2;
        UVChannel clearChannel = UVChannel.uv2;
        UVChannel selectChannel = UVChannel.uv2;
        UVChannel saveChannel = UVChannel.uv2;
        HumanBodyBones setBoneMask;
        HumanBodyBones selectBoneMask;

        enum Plane
        {
            XY,
            XZ,
            YZ,
        }
        Plane selectPlane = Plane.YZ;

		private void OnEnable()
		{
			slotDataAsset = serializedObject.FindProperty("slotDataAsset");
			selectedVerts = serializedObject.FindProperty("selectedVerts");
            selectionColor = serializedObject.FindProperty("selectionColor");
            bitMaskColors = serializedObject.FindProperty("bitMaskColors");

            MeshFilter meshFilter = (target as UVSlotPainter).GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                Debug.LogError("No mesh filter found for this panter object!");

            }
            else
            {
                meshData = meshFilter.sharedMesh;

                vertices = meshData.vertices;
                selectedVerts.arraySize = vertices.Length;
                selection = new bool[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    selection[i] = selectedVerts.GetArrayElementAtIndex(i).boolValue;
                }

                serializedObject.ApplyModifiedProperties();

                vertexLocations = new Dictionary<int, List<int>>();
                for (int i = 0; i < vertices.Length; i++)
                {
                    int hash = vertices[i].GetHashCode();
                    List<int> vertexList;

                    if (vertexLocations.TryGetValue(hash, out vertexList))
                    {
                        vertexList.Add(i);
                    }
                    else
                    {
                        vertexLocations.Add(hash, new List<int> { i });
                    }
                }
            }
		}

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUILayout.Space(20);
            EditorGUILayout.BeginVertical(new GUIStyle("HelpBox"));
            EditorGUILayout.LabelField("Visual Preferences");
            size = EditorGUILayout.Slider("Handle Size", size, 0.01f, 0.1f);
            EditorGUILayout.PropertyField(selectionColor);
            handleChannel = (UVChannel)EditorGUILayout.EnumPopup("Vertex Handle Channel", handleChannel);
            showMaskColors = EditorGUILayout.Foldout(showMaskColors, new GUIContent("Vertex Mask Colors", ""));
            if (showMaskColors)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < bitMaskColors.arraySize; i++)
                {
                    EditorGUILayout.PropertyField(bitMaskColors.GetArrayElementAtIndex(i), new GUIContent(Enum.GetName(typeof(HumanBodyBones), i), ""));
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(20);

            EditorGUILayout.BeginVertical(new GUIStyle("HelpBox"));
            EditorGUILayout.LabelField("Set Vertex Mask Values");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Set Selected Vertices to Mask"))
            {
                int setBitMask = (1 << (int)setBoneMask);
                SetSelectedVerticesMask(meshData, setBitMask, setChannel);
            }
            setChannel = (UVChannel)EditorGUILayout.EnumPopup(setChannel, GUILayout.MaxWidth(80));
            setBoneMask = (HumanBodyBones)EditorGUILayout.EnumPopup(setBoneMask, GUILayout.MaxWidth(80));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Selected Vertices of Mask"))
            {
                ClearSelectedVerticesMask(meshData, clearChannel);
            }
            clearChannel = (UVChannel)EditorGUILayout.EnumPopup(clearChannel, GUILayout.MaxWidth(80));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            GUILayout.Space(20);
            EditorGUILayout.BeginVertical("HelpBox");
            EditorGUILayout.LabelField("Selection Tools");
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Select by Mask"))
            {
                int selectBitMask = (1 << (int)selectBoneMask);
                SelectByMask(meshData, selectBitMask, selectChannel);

            }
            selectChannel = (UVChannel)EditorGUILayout.EnumPopup(selectChannel, GUILayout.MaxWidth(80));
            selectBoneMask = (HumanBodyBones)EditorGUILayout.EnumPopup(selectBoneMask, GUILayout.MaxWidth(80));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Select by Plane Positive"))
            {
                SelectByPlane(true, selectPlane);
            }
            selectPlane = (Plane)EditorGUILayout.EnumPopup(selectPlane, GUILayout.MaxWidth(80));
            if (GUILayout.Button("Select by Plane Negative"))
            {
                SelectByPlane(false, selectPlane);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(true);
            if(GUILayout.Button(new GUIContent("Select By Edge", "Not created!")))
            {
                SelectByEdge(meshData);
            }
            EditorGUI.EndDisabledGroup();
            if(GUILayout.Button("Select All Edges"))
            {
                List<Edge> edges = GetMeshEdges(meshData.vertices, meshData.triangles);
                if (edges != null)
                {
                    ClearSelection();
                    for (int i = 0; i < edges.Count; i++)
                    {
                        int index1 = edges[i].index1;
                        int index2 = edges[i].index2;

                        selection[index1] = true;
                        selectedVerts.GetArrayElementAtIndex(index1).boolValue = true;

                        selection[index2] = true;
                        selectedVerts.GetArrayElementAtIndex(index2).boolValue = true;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Select All"))
            {
                SelectAllVerts();
            }
            if(GUILayout.Button("Invert Selection"))
            {
                InvertSelection();
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Clear Selection", GUILayout.Height(30)))
            {
                ClearSelection();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(20);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save to SlotDataAsset", GUILayout.Height(30)))
            {
                SaveToSlotDataAsset(saveChannel);
            }
            saveChannel = (UVChannel)EditorGUILayout.EnumPopup(saveChannel, GUILayout.MaxWidth(80));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Selected Vertices: " + GetSelectionCount().ToString());

            if (meshData.uv != null && meshData.uv.Length > 0)
            {
                EditorGUILayout.LabelField("UV  Channel exists");
            }
            else
            {
                EditorGUILayout.LabelField("UV  Channel does not exist");
            }
            if (meshData.uv2 != null && meshData.uv2.Length > 0)
            {
                EditorGUILayout.LabelField("UV2 Channel exists");
            }
            else
            {
                EditorGUILayout.LabelField("UV2 Channel does not exist");
            }
            if (meshData.uv3 != null && meshData.uv3.Length > 0)
            {
                EditorGUILayout.LabelField("UV3 Channel exists");
            }
            else
            {
                EditorGUILayout.LabelField("UV3 Channel does not exist");
            }
            if (meshData.uv4 != null && meshData.uv4.Length > 0)
            {
                EditorGUILayout.LabelField("UV4 Channel exists");
            }
            else
            {
                EditorGUILayout.LabelField("UV4 Channel does not exist");
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void SaveToSlotDataAsset(UVChannel channel)
        {
            if(slotDataAsset.objectReferenceValue == null)
            {
                Debug.LogError("No slot data asset found!");
                return;
            }
            
            if(EditorUtility.DisplayDialog("Save", "This will save your changes to " + slotDataAsset.objectReferenceValue.name + " on channel " + channel + " and overwrite any existing data. Are you sure?", "OK", "Cancel"))
            {
                SlotDataAsset slotData = slotDataAsset.objectReferenceValue as SlotDataAsset;
                switch(channel)
                {
                    case UVChannel.uv:
                        slotData.meshData.uv = meshData.uv;
                        break;
                    case UVChannel.uv2:
                        slotData.meshData.uv2 = meshData.uv2;
                        break;
                    case UVChannel.uv3:
                        slotData.meshData.uv3 = meshData.uv3;
                        break;
                    case UVChannel.uv4:
                        slotData.meshData.uv4 = meshData.uv4;
                        break;
                    default:
                        slotData.meshData.uv2 = meshData.uv2;
                        break;
                }
                EditorUtility.SetDirty(slotData);
                AssetDatabase.SaveAssets();
            }
        }

        private int GetSelectionCount()
        {
            //TODO - make this more performant later
            int selectionCount = 0;
            if (selection != null)
            {
                for (int i = 0; i < selection.Length; i++)
                {
                    if (selection[i])
                    {
                        selectionCount++;
                    }
                }
            }
            return selectionCount;
        }

        #region SelectionFunctions
        private void ClearSelection()
        {
            for (int i = 0; i < selectedVerts.arraySize; i++)
            {
                selection[i] = false;
                selectedVerts.GetArrayElementAtIndex(i).boolValue = false;
            }
        }

        private void SelectAllVerts()
        {
            for (int i = 0; i < selectedVerts.arraySize; i++)
            {
                selection[i] = true;
                selectedVerts.GetArrayElementAtIndex(i).boolValue = true;
            }
        }

        private void InvertSelection()
        {
            for (int i = 0; i < selectedVerts.arraySize; i++)
            {
                selection[i] = !selection[i];
                selectedVerts.GetArrayElementAtIndex(i).boolValue = selection[i];
            }
        }

        private void SelectByPlane(bool positive, Plane plane)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                bool select = false;
                if(plane == Plane.XY)
                {
                    if(positive)
                    {
                        if (vertices[i].z >= 0)
                            select = true;
                    }
                    else
                    {
                        if (vertices[i].z <= 0)
                            select = true;
                    }
                }
                if (plane == Plane.XZ)
                {
                    if (positive)
                    {
                        if (vertices[i].y >= 0)
                            select = true;
                    }
                    else
                    {
                        if (vertices[i].y <= 0)
                            select = true;
                    }
                }

                if (plane == Plane.YZ)
                {
                    if (positive)
                    {
                        if (vertices[i].x >= 0)
                            select = true;
                    }
                    else
                    {
                        if (vertices[i].x <= 0)
                            select = true;
                    }
                }

                selection[i] = select;
                selectedVerts.GetArrayElementAtIndex(i).boolValue = select;
            }
        }

        private int FindFirstSelectedIndex()
        {
            for(int i = 0; i < selection.Length; i++)
            {
                if (selection[i])
                    return i;
            }
            return -1;
        }

        private List<Edge> GetMeshEdges(Vector3[] vertices, int[] triangles)
        {
            Dictionary<Edge, int> edges = new Dictionary<Edge, int>();

            for (int i = 0; i < triangles.Length; i += 3)
            {
                var v1 = vertices[triangles[i]];
                var v2 = vertices[triangles[i + 1]];
                var v3 = vertices[triangles[i + 2]];

                Edge edge1 = new Edge(v1, triangles[i], v2, triangles[i + 1]);
                if (edges.ContainsKey(edge1))
                {
                    edges[edge1]++;
                }
                else
                {
                    edges.Add(edge1, 1);
                }

                Edge edge2 = new Edge(v1, triangles[i], v3, triangles[i + 2]);
                if (edges.ContainsKey(edge2))
                {
                    edges[edge2]++;
                }
                else
                {
                    edges.Add(edge2, 1);
                }

                Edge edge3 = new Edge(v2, triangles[i + 1], v3, triangles[i + 2]);
                if (edges.ContainsKey(edge3))
                {
                    edges[edge3]++;
                }
                else
                {
                    edges.Add(edge3, 1);
                }
            }

            List<Edge> borders = new List<Edge>();

            foreach(KeyValuePair<Edge,int> pair in edges)
            {
                if(pair.Value <= 1 && (vertexLocations[pair.Key.v1.GetHashCode()].Count <= 1 || vertexLocations[pair.Key.v2.GetHashCode()].Count <= 1))
                {
                    borders.Add(pair.Key);
                }
            }

            return borders;
        }

        private void SelectByEdge(Mesh meshData)
        {
            int index = FindFirstSelectedIndex();
            if(index >= 0)
            {

            }
        }

        private Vector2[] GetUVChannel(Mesh meshData, UVChannel channel)
        {
            if (meshData == null)
                return null;

            switch (channel)
            {
                case UVChannel.uv:
                    if (meshData.uv == null)
                    {
                        if (EditorUtility.DisplayDialog("No UV Channel", "UV Channel does not exist on this mesh.  Create one?", "OK", "Cancel"))
                        {
                            meshData.uv = new Vector2[meshData.vertices.Length];
                        }
                        else
                        {
                            return null;
                        }
                    }

                    if (meshData.uv.Length < meshData.vertices.Length)
                    {
                        Vector2[] uvs = new Vector2[meshData.vertices.Length];
                        meshData.uv.CopyTo(uvs, 0);
                        meshData.uv = uvs;
                    }
                    return meshData.uv;
                case UVChannel.uv2:
                    if (meshData.uv2 == null)
                    {
                        if (EditorUtility.DisplayDialog("No UV2 Channel", "UV2 Channel does not exist on this mesh.  Create one?", "OK", "Cancel"))
                        {
                            meshData.uv2 = new Vector2[meshData.vertices.Length];
                        }
                        else
                        {
                            return null;
                        }
                    }
                    if (meshData.uv2.Length < meshData.vertices.Length)
                    {
                        Vector2[] uvs = new Vector2[meshData.vertices.Length];
                        meshData.uv2.CopyTo(uvs, 0);
                        meshData.uv2 = uvs;
                    }
                    return meshData.uv2;
                case UVChannel.uv3:
                    if (meshData.uv3 == null)
                    {
                        if (EditorUtility.DisplayDialog("No UV3 Channel", "UV3 Channel does not exist on this mesh.  Create one?", "OK", "Cancel"))
                        {
                            meshData.uv3 = new Vector2[meshData.vertices.Length];
                        }
                        else
                        {
                            return null;
                        }
                    }
                    if (meshData.uv3.Length < meshData.vertices.Length)
                    {
                        Vector2[] uvs = new Vector2[meshData.vertices.Length];
                        meshData.uv3.CopyTo(uvs, 0);
                        meshData.uv3 = uvs;
                    }
                    return meshData.uv3;
                case UVChannel.uv4:
                    if (meshData.uv4 == null)
                    {
                        if (EditorUtility.DisplayDialog("No UV4 Channel", "UV4 Channel does not exist on this mesh.  Create one?", "OK", "Cancel"))
                        {
                            meshData.uv4 = new Vector2[meshData.vertices.Length];
                        }
                        else
                        {
                            return null;
                        }
                    }
                    if (meshData.uv4.Length < meshData.vertices.Length)
                    {
                        Vector2[] uvs = new Vector2[meshData.vertices.Length];
                        meshData.uv4.CopyTo(uvs, 0);
                        meshData.uv4 = uvs;
                    }
                    return meshData.uv4;
                default:
                    if (meshData.uv2 == null)
                    {
                        if (EditorUtility.DisplayDialog("No UV2 Channel", "UV2 Channel does not exist on this mesh.  Create one?", "OK", "Cancel"))
                        {
                            meshData.uv2 = new Vector2[meshData.vertices.Length];
                        }
                        else
                        {
                            return null;
                        }
                    }
                    return meshData.uv2;
            }
        }

        private void SetUVChannel(Mesh meshData, UVChannel channel, Vector2[] uvs)
        {
            if (meshData == null)
                return;

            switch (channel)
            {
                case UVChannel.uv:
                    meshData.uv = uvs;
                    break;
                case UVChannel.uv2:
                    meshData.uv2 = uvs;
                    break;
                case UVChannel.uv3:
                    meshData.uv3 = uvs;
                    break;
                case UVChannel.uv4:
                    meshData.uv4 = uvs;
                    break;
                default:
                    meshData.uv2 = uvs;
                    break;
            }
        }
        
        private void ClearSelectedVerticesMask(Mesh meshData, UVChannel channel)
        {
            Vector2[] uvs = GetUVChannel(meshData, channel);
            if (uvs == null)
                return;

            for (int i = 0; i < selection.Length; i++)
            {
                if (selection[i])
                {
                    uvs[i].x = 0;
                }
            }

            SetUVChannel(meshData, channel, uvs);
            AssetDatabase.SaveAssets();
        }

        private void SetSelectedVerticesMask(Mesh meshData, int bitMask, UVChannel channel)
        {
            Vector2[] uvs = GetUVChannel(meshData, channel);

            if (uvs == null)
            {
                Debug.LogError("UVs are null!");
                return;
            }

            for (int i = 0; i < selection.Length; i++)
            {
                if (selection[i])
                {
                    uvs[i].x = bitMask;
                }
            }

            SetUVChannel(meshData, channel, uvs);
            AssetDatabase.SaveAssets();
        }

        private void SelectByMask(Mesh meshData, int bitMask, UVChannel channel)
        {
            Vector2[] uvs = GetUVChannel(meshData, channel);
            if (uvs == null)
                return;

            for (int i = 0; i < selectedVerts.arraySize; i++)
            {
                selection[i] = ((int)uvs[i].x & bitMask) != 0;
                selectedVerts.GetArrayElementAtIndex(i).boolValue = selection[i];
            }
        }
        #endregion

        void OnSceneGUI()
        {
            UVSlotPainter painter = (target as UVSlotPainter);
            Selection.activeGameObject = painter.gameObject; //so we prevent deselecting the object when clicking in the scene view.

            if (vertices == null)
            {
                Debug.LogError("No vertices found!");
                return;
            }

            serializedObject.Update();

            Transform mat = painter.transform;
            Vector2[] uvs = GetUVChannel(meshData, handleChannel);

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 point = mat.TransformPoint(vertices[i]);
                if (selection[i])
                {
                    Handles.color = selectionColor.colorValue;
                }
                else
                {
                    if (uvs != null)
                    {
                        int mask = (int)uvs[i].x;

                        if (mask > 0)
                        {
                            mask = (int)Mathf.Log(mask, 2);
                            //Handles.color = bitMaskColors.GetArrayElementAtIndex(mask).colorValue; //slow
                            Handles.color = painter.bitMaskColors[mask];
                        }
                        else
                        {
                            Handles.color = Color.black;
                        }                                
                    }
                    else
                    {
                        Handles.color = Color.black;
                    }
                }

                if(Handles.Button(point, Quaternion.identity, HandleUtility.GetHandleSize(point) * size, HandleUtility.GetHandleSize(point) * size, Handles.DotHandleCap ))
                {
					selection[i] = !selection[i];
                    //selectedVerts.GetArrayElementAtIndex(i).boolValue = selection[i]; //update serialized backer.

					int hash = vertices[i].GetHashCode();
					List<int> vertexList;
					if (vertexLocations.TryGetValue(hash, out vertexList))
					{
						foreach (int vert in vertexList)
						{
							selection[vert] = selection[i];
							selectedVerts.GetArrayElementAtIndex(vert).boolValue = selection[i]; //update serialized backer.
						}
					}
				}
			}
            

            serializedObject.ApplyModifiedProperties();
        }
    }
}
