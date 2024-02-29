#if !EA_ONBUILD
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static ShaderEditorlabels;
using EAUploader;
using EAUploader.CustomPrefabUtility;
using static styles;
using static Texture;
using UnityEngine.UIElements;
using EAUploader.UI.Components;
using System;

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

    private static GameObject selectedPrefabInstance;
    private static Preview preview;

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
        ShaderEditor window = GetWindow<ShaderEditor>();
        window.titleContent = new GUIContent(WindowName);
        window.Show();
        window.maximized = true;
    }

    private void CreateGUI()
    {
        string prefabPath = EAUploaderCore.selectedPrefabPath;

        if (prefabPath == null)
        {
            bool userClickedOk = EditorUtility.DisplayDialog(
            Dialog1,
            Dialog2,
            OK
            );
            Close();
        }

        selectedPrefabInstance = PrefabUtility.LoadPrefabContents(prefabPath);

        if (selectedPrefabInstance != null)
        {
            Renderer[] renderers = selectedPrefabInstance.GetComponentsInChildren<Renderer>();
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


        var root = rootVisualElement;

        rootVisualElement.styleSheets.Add(EAUploader.UI.EAUploader.tailwind);
        rootVisualElement.styleSheets.Add(EAUploader.UI.EAUploader.styles); 

        root.style.flexGrow = 1;

        var container = new VisualElement()
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                flexGrow = 1
            }
        };

        root.Add(container);

        var previewContainer = new VisualElement()
        {
            style =
            {
                flexGrow = 1,
                width = new StyleLength(new Length(50, LengthUnit.Percent))
            }
        };
        previewContainer.AddToClassList("border-r");
        previewContainer.AddToClassList("border-r-zinc-300");

        container.Add(previewContainer);

        preview = new Preview(previewContainer, EAUploaderCore.selectedPrefabPath);
        preview.ShowContent();

        var shaderContainer = new VisualElement()
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                flexGrow = 1,
                width = new StyleLength(new Length(50, LengthUnit.Percent))
            }
        };

        var shaderEditor = new VisualElement()
        {
            style =
            {
                flexGrow = 1,
                flexShrink = 1
            }
        };
        shaderEditor.AddToClassList("border-r");
        shaderEditor.AddToClassList("border-r-zinc-300");

        shaderContainer.Add(shaderEditor);

        var shaderList = new VisualElement()
        {
            style =
            {
                flexGrow = 1,
                flexShrink = 1
            }
        };

        shaderContainer.Add(shaderList);

        container.Add(shaderContainer);


        var closeButton = new EAUploader.UI.Components.ShadowButton()
        {
            text = CloseButtonLabel
        };

        closeButton.clicked += () =>
        {
            Close();
        };

        root.Add(closeButton);


        var shaderOptions = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Shader"))
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
        
        var shaderDropdown = new DropdownField()
        {
            label = EditShader,
            choices = shaderOptions.Select(x => x).ToList(),
            index = selectedShaderIndex
        };

        shaderDropdown.RegisterValueChangedCallback((evt) =>
        {
            selectedShaderIndex = Int32.Parse(evt.newValue);
        });

        shaderEditor.Add(shaderDropdown);

        var applyAllButton = new EAUploader.UI.Components.ShadowButton()
        {
            text = ApplyAll
        };

        applyAllButton.clicked += () =>
        {
            newShader = Shader.Find(shaderOptions[selectedShaderIndex]);
            ApplyShaderToAll(newShader);
        };

        var undoButton = new EAUploader.UI.Components.ShadowButton()
        {
            text = UndoLabel
        };

        undoButton.clicked += () =>
        {
            UndoShaderChanges();
        };

        var saveButton = new EAUploader.UI.Components.ShadowButton()
        {
            text = Save
        };

        saveButton.clicked += () =>
        {
            SaveCurrentShaders();
        };


        shaderEditor.Add(applyAllButton);
        shaderEditor.Add(undoButton);
        shaderEditor.Add(saveButton);


        var shaderEditorContainer = new ScrollView() 
        {
            style =
            {
                flexGrow = 1,
                flexShrink = 1
            }
        };
        shaderEditor.Add(shaderEditorContainer);

        var shaderEditorContent = new VisualElement()
        {
            style =
            {
                flexDirection = FlexDirection.Column
            }
        };

        shaderEditorContainer.Add(shaderEditorContent);

        foreach (var mat in materials)
        {
            var shaderItem = new VisualElement()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            var shaderPreview = new Image()
            {
                image = AssetPreview.GetAssetPreview(mat),
                scaleMode = ScaleMode.ScaleToFit,
                style =
                {
                    width = 50,
                    height = 50
                }
            };

            shaderItem.Add(shaderPreview);

            var shaderName = new Label()
            {
                text = mat.name,
                style =
                {
                    marginLeft = 10
                }
            };

            shaderItem.Add(shaderName);

            var shaderDropdownItem = new DropdownField()
            {
                choices = shaderOptions.Select(x => x).ToList(),
                index = shaderOptions.IndexOf(mat.shader.name)
            };

            shaderDropdownItem.RegisterValueChangedCallback((evt) =>
            {
                SaveOriginalShader(mat);
                mat.shader = Shader.Find(evt.newValue);
                shaderChanged = true;
            });

            shaderItem.Add(shaderDropdownItem);

            shaderEditorContent.Add(shaderItem);
        }

        var manageArea = new VisualElement()
        {
            style =
            {
                flexDirection = FlexDirection.Column,
                flexGrow = 1,
                flexShrink = 1
            }
        };

        shaderList.Add(manageArea);

        var manageLabel = new Label()
        {
            text = Shaders,
            style =
            {
                fontSize = 24,
                marginBottom = 10
            }
        };

        manageArea.Add(manageLabel);

        var shaderGroups = new Dictionary<string, List<string>>();

        foreach (string shaderName in shaderOptions)
        {
            string groupName = shaderName.Split('/')[0].Trim();
            if (!shaderGroups.ContainsKey(groupName))
            {
                shaderGroups[groupName] = new List<string>();
            }
            shaderGroups[groupName].Add(shaderName);
        }

        var shaderGroupList = new ScrollView()
        {
            style =
            {
                flexGrow = 1,
                flexShrink = 1
            }
        };

        manageArea.Add(shaderGroupList);

        var shaderGroupContent = new VisualElement()
        {
            style =
            {
                flexDirection = FlexDirection.Column
            }
        };

        shaderGroupList.Add(shaderGroupContent);

        foreach (var group in shaderGroups)
        {
            if (!excludedShaders.Contains(group.Key))
            {
                var groupLabel = new Label()
                {
                    text = group.Key,
                    style =
                    {
                        fontSize = 18,
                        marginBottom = 10
                    }
                };

                shaderGroupContent.Add(groupLabel);
            }
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
#endif