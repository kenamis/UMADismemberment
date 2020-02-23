using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UMA;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace UMA.Dismemberment2
{
    public class UVSlotPainter : MonoBehaviour
    {
		public SlotDataAsset slotDataAsset;

		public bool[] selectedVerts = new bool[0];

#if UNITY_EDITOR
		[MenuItem("CONTEXT/SlotDataAsset/Begin UV Painting")]
        static void BeginPainting(MenuCommand command)
        {
			SlotDataAsset slotDataAsset = (SlotDataAsset)command.context;

			int saveChoice = EditorUtility.DisplayDialogComplex("Open Mesh Hide Editor", "Opening the Mesh Hide Editor will close all scenes and create a new blank scene. Any current scene changes will be lost unless saved.", "Save and Continue", "Continue without saving", "Cancel");

			switch (saveChoice)
			{
				case 0: // Save and continue
					{
						if (!EditorSceneManager.SaveOpenScenes())
							return;
						break;
					}
				case 1: // don't save and continue
					break;
				case 2: // cancel
					return;
			}

			SceneView sceneView = SceneView.lastActiveSceneView;

			if (sceneView == null)
			{
				EditorUtility.DisplayDialog("Error", "A Scene View must be open and active", "OK");
				return;
			}

			SceneView.lastActiveSceneView.Focus();

			//List<GeometrySelector.SceneInfo> currentscenes = new List<GeometrySelector.SceneInfo>();

			for (int i = 0; i < EditorSceneManager.sceneCount; i++)
			{
				Scene sc = EditorSceneManager.GetSceneAt(i);
				GeometrySelector.SceneInfo si = new GeometrySelector.SceneInfo();
				si.path = sc.path;
				si.name = sc.name;
				if (i == 0)
				{
					// first scene should clear the temp scene.
					si.mode = OpenSceneMode.Single;
				}
				else
				{
					si.mode = sc.isLoaded ? OpenSceneMode.Additive : OpenSceneMode.AdditiveWithoutLoading;
				}
				//currentscenes.Add(si);
			}

			Scene s = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
			EditorSceneManager.SetActiveScene(s);

			GameObject obj = EditorUtility.CreateGameObjectWithHideFlags("Selector", HideFlags.DontSaveInEditor, typeof(MeshFilter), typeof(MeshRenderer), typeof(UVSlotPainter));
			MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
			meshFilter.hideFlags = HideFlags.HideInInspector;
			MeshRenderer meshRenderer = obj.GetComponent<MeshRenderer>();
			meshRenderer.hideFlags = HideFlags.HideInInspector;
			UVSlotPainter slotPainter = obj.GetComponent<UVSlotPainter>();
			slotPainter.slotDataAsset = slotDataAsset;

			Selection.activeObject = obj;

			Mesh mesh = new Mesh();
			mesh.vertices = slotDataAsset.meshData.vertices;

			mesh.uv = slotDataAsset.meshData.uv;
			if(slotDataAsset.meshData.uv2 != null)
			{
				mesh.uv2 = slotDataAsset.meshData.uv2;
			}
			if (slotDataAsset.meshData.uv3 != null)
			{
				mesh.uv3 = slotDataAsset.meshData.uv3;
			}
			if (slotDataAsset.meshData.uv4 != null)
			{
				mesh.uv4 = slotDataAsset.meshData.uv4;
			}
			mesh.SetTriangles(slotDataAsset.meshData.submeshes[slotDataAsset.subMeshIndex].triangles, slotDataAsset.subMeshIndex);

			meshFilter.sharedMesh = mesh;
			meshRenderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
		}
#endif
	}
}
