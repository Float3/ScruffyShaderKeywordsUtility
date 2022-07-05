// Written by ScruffyRules#0879
// Thank you to Xiexe and all that tested!
// Licensed under the MIT License (see https://vrchat.com/legal/attribution)

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDKBase;

public class ShaderKeywordsUtility : EditorWindow
{
	private static Dictionary<VRC_AvatarDescriptor, Dictionary<Material, bool>> avatars =
		new Dictionary<VRC_AvatarDescriptor, Dictionary<Material, bool>>();

	private static Dictionary<Shader, HashSet<string>> shaderCache = new Dictionary<Shader, HashSet<string>>();

	private Dictionary<VRC_AvatarDescriptor, bool> avatarsOpened =
		new Dictionary<VRC_AvatarDescriptor, bool>();

	private Vector2 scrollPos;
	private static GUIStyle titleGuiStyle;

	public static HashSet<string> keywordBlacklist = new HashSet<string>(new[]
	{
		// Global Unity Keywords, these don't matter at all. (They should be loaded)
		// All Global Keywords that are in Standard Unity Shaders
		"BILLBOARD_FACE_CAMERA_POS",
		"EDITOR_VISUALIZATION",
		"ETC1_EXTERNAL_ALPHA",
		"FOG_EXP",
		"FOG_EXP2",
		"FOG_LINEAR",
		"LOD_FADE_CROSSFADE",
		"OUTLINE_ON",
		"SHADOWS_SHADOWMASK",
		"SOFTPARTICLES_ON",
		"STEREO_INSTANCING_ON",
		"STEREO_MULTIVIEW_ON",
		"UNITY_HDR_ON",
		"UNITY_SINGLE_PASS_STEREO",
		"VERTEXLIGHT_ON",
		"_EMISSION",
		"UNDERLAY_ON", //these are commented out in the builtin shaders but are also used by TMP shaders
		"UNDERLAY_INNER", 
		// Post Processing Stack V1 and V2
		// This is mostly just safe keeping somewhere
		"APPLY_FORWARD_FOG",
		"AUTO_EXPOSURE",
		"BLOOM",
		"BLOOM_LOW",
		"CHROMATIC_ABERRATION",
		"CHROMATIC_ABERRATION_LOW",
		"COLOR_GRADING_HDR",
		"COLOR_GRADING_HDR_2D",
		"COLOR_GRADING_HDR_3D",
		"COLOR_GRADING_LDR_2D",
		"DISTORT",
		"FINALPASS",
		"FOG_EXP",
		"FOG_EXP2",
		"FOG_LINEAR",
		"FXAA",
		"FXAA_KEEP_ALPHA",
		"FXAA_LOW",
		"FXAA_NO_ALPHA",
		"GRAIN",
		"SOURCE_GBUFFER",
		"STEREO_DOUBLEWIDE_TARGET",
		"STEREO_INSTANCING_ENABLED",
		"TONEMAPPING_ACES",
		"TONEMAPPING_CUSTOM",
		"TONEMAPPING_NEUTRAL",
		"VIGNETTE",
	});

	const string keywordDescription =
		"Unity has a limit of 384 global keywords. A lot (~40) are used internally by Unity.\n\nAny new global keyword you encounter goes onto the global list, and will stay until you restart the client.\n\nKeywords are used to create compile time branches and remove code, to optimize a shader, however, because of the 256 keyword limit, using them in VRChat can cause other shaders which use keywords to break, as once you hit the limit, any new keyword will get ignored.\n\nIt's best in the confines of VRChat to stay away from using custom keywords if possible, as not to cause issues with (your) shaders breaking.\n\nFor the full list of internal keywords, see 'https://gist.github.com/Float3/afce1f343d3c5f912fb8943bf25f7fcb'";

	private static bool avatarsDirty = true;
	private int loadedScenes;

	[MenuItem("VRChat SDK/Utilities/Avatar Shader Keywords Utility", false, 990)]
	static void Init()
	{
		ShaderKeywordsUtility window = GetWindow<ShaderKeywordsUtility>();
		window.titleContent = new GUIContent("Shader Keywords Utility");
		window.minSize = new Vector2(325, 410);
		window.Show();

		titleGuiStyle = new GUIStyle
		{
			fontSize = 15,
			fontStyle = FontStyle.BoldAndItalic,
			alignment = TextAnchor.MiddleCenter,
			wordWrap = true
		};

		if (EditorGUIUtility.isProSkin)
			titleGuiStyle.normal.textColor = Color.white;
		else
			titleGuiStyle.normal.textColor = Color.black;
	}

	public static List<VRC_AvatarDescriptor> getADescs()
	{
		List<GameObject> GOs = new List<GameObject>();
		for (int i = 0; i < EditorSceneManager.sceneCount; i++)
		{
			Scene scene = EditorSceneManager.GetSceneAt(i);
			if (scene.isLoaded)
			{
				GameObject[] GOs2 = scene.GetRootGameObjects();
				foreach (GameObject go in GOs2)
				{
					GOs.Add(go);
				}
			}
		}

		List<VRC_AvatarDescriptor> descriptors = new List<VRC_AvatarDescriptor>();
		foreach (GameObject go in GOs)
		{
			var vrcdescs = go.GetComponentsInChildren<VRC_AvatarDescriptor>(true);
			foreach (VRC_AvatarDescriptor vrcdesc in vrcdescs)
			{
				descriptors.Add(vrcdesc);
			}
		}

		return descriptors;
	}

	public static bool DetectCustomShaderKeywords(VRC_AvatarDescriptor ad)
	{
		foreach (Renderer renderer in ad.transform.GetComponentsInChildren<Renderer>(true))
		{
			foreach (Material mat in renderer.sharedMaterials)
			{
				if (mat != null)
				{
					string[] localkeywords = GetLocalKeywords(mat.shader);
					foreach (string keyword in mat.shaderKeywords)
					{
						if (!keywordBlacklist.Contains(keyword) && !localkeywords.Contains(keyword))
							return true;
					}
				}
			}
		}

		return false;
	}

	public static string[] GetLocalKeywords(Shader shader)
	{
		if (shaderCache.ContainsKey(shader))
		{
			return shaderCache[shader].ToArray();
		}

		IEnumerable<SerializedProperty> GetArray(SerializedProperty x)
		{
			int count = x.arraySize;
			SerializedProperty[] children = new SerializedProperty[count];
			for (int i = 0; i < count; i++)
				children[i] = x.GetArrayElementAtIndex(i);

			return children;
		}

		List<SerializedProperty> props = new List<SerializedProperty>();
		using (SerializedProperty iterator = new SerializedObject(shader).GetIterator())
		{
			while (iterator.Next(true))
			{
				if (iterator.name.Contains("m_VariantsUserLocal"))
					props.Add(iterator.Copy());
			}
		}

		string[] localkeywordList = props.SelectMany(GetArray)
			.SelectMany(GetArray)
			.Select(x => x.stringValue)
			.Distinct().ToArray();

		shaderCache.Add(shader, new HashSet<string>(localkeywordList));

		return localkeywordList;
	}

	void OnGUI()
	{
		GUILayout.Space(10);
		GUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		GUILayout.Label("Shader Keywords Utility", titleGuiStyle);
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();
		GUILayout.Space(15);

		bool showHelp = EditorPrefs.GetBool("VRCSDK_ShowShaderKeywordsHelp", true);
		if (showHelp)
		{
			GUILayout.Label(keywordDescription, EditorStyles.helpBox, GUILayout.ExpandWidth(true));
			GUILayout.Space(15);
		}

		int _loadedScenes = 0;
		for (int i = 0; i < EditorSceneManager.sceneCount; i++)
		{
			Scene scene = EditorSceneManager.GetSceneAt(i);
			if (scene.isLoaded)
				_loadedScenes += 1;
		}

		if (_loadedScenes != loadedScenes)
		{
			// Debug.Log("Loaded Scenes changed");
			loadedScenes = _loadedScenes;
			avatarsDirty = true;
		}

		GUILayout.BeginHorizontal();
		if (GUILayout.Button("Refresh Avatars", GUILayout.ExpandWidth(false)))
			avatarsDirty = true;
		GUILayout.FlexibleSpace();
		if (GUILayout.Button((showHelp ? "Hide" : "Show") + " Info about Keywords"))
		{
			showHelp = !showHelp;
			EditorPrefs.SetBool("VRCSDK_ShowShaderKeywordsHelp", showHelp);
		}

		GUILayout.EndHorizontal();
		GUILayout.Space(5);

		scrollPos = EditorGUILayout.BeginScrollView(scrollPos, false, false);
		ListAvatars();
		EditorGUILayout.EndScrollView();
	}

	void ListAvatars()
	{
		if (avatarsDirty)
			CacheAvatars();

		Dictionary<VRC_AvatarDescriptor, Dictionary<Material, bool>> avatarsE =
			new Dictionary<VRC_AvatarDescriptor, Dictionary<Material, bool>>(avatars);
		foreach (VRC_AvatarDescriptor vrcAD in avatarsE.Keys)
		{
			List<string> keywords = new List<string>();
			foreach (Material mat in avatars[vrcAD].Keys)
			{
				string[] localkeywords = GetLocalKeywords(mat.shader);

				foreach (string keyword in mat.shaderKeywords)
				{
					if (!keywords.Contains(keyword) && !keywordBlacklist.Contains(keyword) &&
					    !localkeywords.Contains(keyword))
						keywords.Add(keyword);
				}
			}

			if (keywords.Count == 0)
			{
				avatars.Remove(vrcAD);
				avatarsOpened.Remove(vrcAD);
				continue;
			}

			GUILayout.BeginHorizontal();
			bool avatarOpened = avatarsOpened[vrcAD];
			avatarOpened = EditorGUILayout.ToggleLeft("", avatarOpened, GUILayout.MaxWidth(15f));
			avatarsOpened[vrcAD] = avatarOpened;
			EditorGUILayout.ObjectField(vrcAD, typeof(VRC_AvatarDescriptor), true);
			GUILayout.EndHorizontal();

			if (avatarOpened)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Space(23.0879f);
				GUILayout.Label("Total Custom Keywords on Avatar: " + keywords.Count);
				GUILayout.EndHorizontal();

				Dictionary<Material, bool> materials = new Dictionary<Material, bool>(avatars[vrcAD]);
				foreach (KeyValuePair<Material, bool> matKeyVal in materials)
				{
					Material material = matKeyVal.Key;
					bool materialOpened = matKeyVal.Value;

					GUILayout.BeginHorizontal();
					GUILayout.Space(23.0879f);
					materialOpened = EditorGUILayout.ToggleLeft("", materialOpened, GUILayout.MaxWidth(15f));
					avatars[vrcAD][material] = materialOpened;
					EditorGUILayout.ObjectField(material, typeof(Material), false);
					GUILayout.EndHorizontal();

					if (materialOpened)
					{
						GUILayout.BeginHorizontal();
						GUILayout.Space(23.0879f * 2f);
						if (GUILayout.Button("Delete ALL Keywords on this Material"))
						{
							if (EditorUtility.DisplayDialog("Delete All Keywords on this Material",
								"Are you sure you want to delete all Shader Keywords on this material?\nSome shaders might use these!",
								"Yes", "No"))
							{
								foreach (string keyword in material.shaderKeywords)
								{
									if (!keywordBlacklist.Contains(keyword) && !shaderCache[material.shader].Contains(keyword))
										material.DisableKeyword(keyword);
								}

								avatars[vrcAD].Remove(material);
							}
						}

						GUILayout.EndHorizontal();

						GUILayout.BeginHorizontal();
						GUILayout.Space(23.0879f * 2f);
						GUILayout.Label("Keywords", EditorStyles.boldLabel);
						GUILayout.EndHorizontal();

						int keywordsCount = 0;
						foreach (string keyword in material.shaderKeywords)
						{
							if (!keywordBlacklist.Contains(keyword))
							{
								keywordsCount++;
								GUILayout.BeginHorizontal();
								GUILayout.Space(23.0879f * 2f);
								GUILayout.Label(keyword);
								if (GUILayout.Button("Delete", GUILayout.ExpandWidth(false)))
									material.DisableKeyword(keyword);
								GUILayout.EndHorizontal();
							}
						}

						if (keywordsCount == 0)
							avatars[vrcAD].Remove(material);
					}
				}

				GUILayout.Space(2f);
			}
		}
	}

	void CacheAvatars()
	{
		//Debug.Log("Caching avatars");
		avatars = new Dictionary<VRC_AvatarDescriptor, Dictionary<Material, bool>>();

		List<VRC_AvatarDescriptor> avatarDescriptors = getADescs();

		foreach (VRC_AvatarDescriptor aD in avatarDescriptors)
		{
			if (!avatars.ContainsKey(aD))
				avatars.Add(aD, new Dictionary<Material, bool>());

			if (!avatarsOpened.ContainsKey(aD))
				avatarsOpened.Add(aD, false);

			foreach (Renderer renderer in aD.transform.GetComponentsInChildren<Renderer>(true))
			{
				foreach (Material mat in renderer.sharedMaterials)
				{
					if (mat != null)
					{
						if (!avatars[aD].ContainsKey(mat))
						{
							foreach (string keyword in mat.shaderKeywords)
							{
								if (!keywordBlacklist.Contains(keyword))
								{
									avatars[aD].Add(mat, false);
									break;
								}
							}
						}
					}
				}
			}

			if (avatars[aD].Count == 0)
			{
				avatars.Remove(aD);
				avatarsOpened.Remove(aD);
			}
		}

		// prevent leaking
		Dictionary<VRC_AvatarDescriptor, bool> avatarsOpenedE =
			new Dictionary<VRC_AvatarDescriptor, bool>();
		foreach (VRC_AvatarDescriptor aO in avatarsOpenedE.Keys)
		{
			if (!avatarDescriptors.Contains(aO))
				avatarsOpened.Remove(aO);
		}

		avatarsDirty = false;
	}
}
#endif
