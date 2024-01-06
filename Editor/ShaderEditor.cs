using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static ShaderEditorlabels;
using EAUploaderEditors;
using static styles;
using static Texture;

public static class ShaderEditorMenu
{
    [MenuItem("EAUploader/Shader Editor")]
    public static void ShowShaderEditorWindow()
    {
        ShaderEditor.Open();
    }
}

public class ShaderEditor : EditorWindow
{
    private Shader newShader;
    private List<Material> materials = new List<Material>();
    private int selectedShaderIndex = 0;
    private bool shaderChanged = false;
    private static Vector2 ScrollPosition;
    private GUIStyle sLabelStyle;
    private GUIStyle boxStyle;
    private static Editor gameObjectEditor;
    private Vector2 _scrollPosition;
    private static GameObject currentPreviewObject;
    private Dictionary<Material, Shader> originalShaders = new Dictionary<Material, Shader>(); 
    // 除外Shader
    private List<string> excludedShaders;
    
    [InitializeOnLoadMethod]
    private static void InitializeOnLoad()
    {
        var editorRegistration = new EditorRegistration
        {
            MenuName = "EAUploader/Shader Editor",
            EditorName = WindowName,
            Description = WindowDescription,
            Version = "1.0",
            Author = "EAUploader",
            Url = "https://uslog.tech/eauploader"
        };

        EAUploaderEditorManager.RegisterEditor(editorRegistration);
    }

    public static void Open()
    {
        ShaderEditorlabels.UpdateLanguage();
        ShaderEditor window = (ShaderEditor)EditorWindow.GetWindow(typeof(ShaderEditor), false, "Shader Editor");
        window.Show();
        window.maximized = true;
    }

    private void OnEnable()
    {
        if (CustomPrefabUtility.selectedPrefabInstance != null)
        {
            Renderer[] renderers = CustomPrefabUtility.selectedPrefabInstance.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat != null && !materials.Contains(mat) && !mat.name.Contains("EyeIris"))
                    {
                        materials.Add(mat);
                    }
                }
            }
            excludedShaders = LoadExcludedShaderGroups();
        }
        ShaderChecker.CheckShadersInPrefabs();
    }

    private void OnGUI()
    {
        if (sLabelStyle == null)
        {
            sLabelStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.black } };
        }
        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box) 
            { 
                fixedWidth = 60, 
                fixedHeight = 60,
                imagePosition = ImagePosition.ImageOnly
            };
        }
        if (CustomPrefabUtility.selectedPrefabInstance == null)
        {
            bool userClickedOk = EditorUtility.DisplayDialog(
            Dialog1,
            Dialog2,
            OK
            );
            Close();
        }

        EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), Color.white);

        float totalWidth = this.position.width;
        float totalHeight = this.position.height;

        float upperPartHeight = totalHeight * 0.95f;
        float lowerPartHeight = totalHeight * 0.05f;
        float leftWidth = totalWidth * 0.5f;
        float middleWidth = totalWidth * 0.3f;
        float rightWidth = totalWidth * 0.2f;

        float previewAreaWidth = leftWidth;
        float previewAreaHeight = upperPartHeight;

        Rect upperPartRect = new Rect(0, 0, leftWidth, upperPartHeight);
        GUILayout.BeginArea(upperPartRect);
        if (CustomPrefabUtility.selectedPrefabInstance != null)
        {
            if (currentPreviewObject != CustomPrefabUtility.selectedPrefabInstance)
            {
                if (gameObjectEditor != null)
                {
                    UnityEngine.Object.DestroyImmediate(gameObjectEditor);
                }

                currentPreviewObject = CustomPrefabUtility.selectedPrefabInstance;
                gameObjectEditor = Editor.CreateEditor(currentPreviewObject);
            }

            if (gameObjectEditor != null)
            {
                GUIStyle bgColor = new GUIStyle();
                bgColor.normal.background = EditorGUIUtility.whiteTexture;

                gameObjectEditor.OnInteractivePreviewGUI(upperPartRect, bgColor);
            }
        }
        GUILayout.EndArea();

        EditorGUI.DrawRect(new Rect(leftWidth, 0, 1f, upperPartHeight), Color.black);
        EditorGUI.DrawRect(new Rect(leftWidth + middleWidth, 0, 1f, upperPartHeight), Color.black);

        GUILayout.BeginArea(new Rect(leftWidth + 1f, 0, middleWidth - 1f, upperPartHeight));
        GUILayout.Label(EditShader, h1LabelStyle);
        string[] shaderGuids = AssetDatabase.FindAssets("t:Shader");
        List<string> shaderOptions = new List<string>();
        foreach (var guid in shaderGuids)
        {
            string shaderPath = AssetDatabase.GUIDToAssetPath(guid);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            string shaderGroupName = shader.name.Split('/')[0].Trim(); // シェーダー名からグループ名を取得

            if (!excludedShaders.Contains(shaderGroupName))
            {
                shaderOptions.Add(shader.name);
            }
        }

        int prevSelectedShaderIndex = selectedShaderIndex;
        selectedShaderIndex = EditorGUILayout.Popup(selectedShaderIndex, shaderOptions.ToArray(), PopupStyle);
        if (GUILayout.Button(ApplyAll, SubButtonStyle))
        {
            Shader selectedShader = Shader.Find(shaderOptions[selectedShaderIndex]);
            ApplyShaderToAll(selectedShader);
        }

        GUILayout.BeginHorizontal();
        // "Undo" 
        if (shaderChanged && GUILayout.Button(UndoLabel, SubButtonStyle))
        {
            UndoShaderChanges();
        }

        // "Save"
        if (shaderChanged && GUILayout.Button(Save, SubButtonStyle))
        {
            SaveCurrentShaders();
        }
        GUILayout.EndHorizontal();

        ScrollPosition = GUILayout.BeginScrollView(ScrollPosition);
        foreach (var mat in materials)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Box(AssetPreview.GetAssetPreview(mat), boxStyle);
            EditorGUILayout.LabelField(mat.name, sLabelStyle);

            string currentShaderName = mat.shader.name;
            int currentIndex = shaderOptions.IndexOf(currentShaderName);
            int newIndex = EditorGUILayout.Popup(currentIndex, shaderOptions.ToArray(), PopupStyle);

            if (newIndex != currentIndex)
            {
                SaveOriginalShader(mat);
                mat.shader = Shader.Find(shaderOptions[newIndex]);
                shaderChanged = true;
            }
            EditorGUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();

        if (shaderChanged)
        {
            Repaint();
        }

        GUILayout.EndArea();

        Rect ManageArea = new Rect(leftWidth + middleWidth + 1f, 0, rightWidth - 1f, upperPartHeight);
        GUILayout.BeginArea(ManageArea);
        GUILayout.Label(Shaders, h1LabelStyle);
        DrawHorizontalLine(Color.black, 12, ManageArea.width);

        // シェーダーをグループ別に整理
        Dictionary<string, List<string>> shaderGroups = new Dictionary<string, List<string>>();
        foreach (string shaderName in shaderOptions)
        {
            string groupName = shaderName.Split('/')[0].Trim();
            if (!shaderGroups.ContainsKey(groupName))
            {
                shaderGroups[groupName] = new List<string>();
            }
            shaderGroups[groupName].Add(shaderName);
        }

        GUILayout.BeginVertical();
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
        foreach (var group in shaderGroups)
        {
            if (!excludedShaders.Contains(group.Key))
            {
                GUILayout.Label(group.Key, h4LabelStyle);
            }

            DrawHorizontalDottedLine(Color.black, 12, ManageArea.width);
        }
        GUILayout.EndScrollView();

        if (GUILayout.Button(Opendetail, SubButtonStyle))
        {
            Application.OpenURL("https://www.uslog.tech/eauploader-forum/__q-a/siedanituite");
        }

        GUILayout.EndVertical();

        GUILayout.EndArea();

        Rect closeButtonRect = new Rect(0, upperPartHeight, totalWidth, lowerPartHeight);
        GUILayout.BeginArea(closeButtonRect);
        if (GUILayout.Button(CloseButtonLabel, MainButtonStyle))
        {
            Close();
        }
        GUILayout.EndArea();

        if (shaderChanged)
        {
            UpdatePrefabPreview();
            shaderChanged = false;
        }
    }

    private void SaveOriginalShader(Material mat)
    {
        if (!originalShaders.ContainsKey(mat))
        {
            originalShaders[mat] = mat.shader;
        }
    }

    private void SaveCurrentShaders()
    {
        originalShaders.Clear();
        foreach (var mat in materials)
        {
            originalShaders[mat] = mat.shader;
        }
        shaderChanged = false;
    }

    private void ApplyShaderToAll(Shader newShader)
    {
        foreach (var mat in materials)
        {
            SaveOriginalShader(mat);
            mat.shader = newShader;
        }
        shaderChanged = true;
    }

    private void UndoShaderChanges()
    {
        foreach (var mat in materials)
        {
            if (originalShaders.ContainsKey(mat))
            {
                mat.shader = originalShaders[mat];
            }
        }
        shaderChanged = false;
    }

    private List<string> LoadExcludedShaderGroups()
    {
        string jsonPath = "Packages/tech.uslog.shadereditor-for-eauploader/Editor/ExcludedShaders.json";
        if (File.Exists(jsonPath))
        {
            string jsonContent = File.ReadAllText(jsonPath);
            ExcludedShaderGroupList excludedList = JsonUtility.FromJson<ExcludedShaderGroupList>(jsonContent);
            return excludedList.excludedShaderGroups;
        }
        return new List<string>();
    }

    private void UpdatePrefabPreview()
    {
        if (currentPreviewObject != null && gameObjectEditor != null)
        {
            // gameObjectEditorの再作成
            UnityEngine.Object.DestroyImmediate(gameObjectEditor);
            gameObjectEditor = Editor.CreateEditor(currentPreviewObject);
        }
    }

    [System.Serializable]
    private class ExcludedShaderGroupList
    {
        public List<string> excludedShaderGroups;
    }
}
