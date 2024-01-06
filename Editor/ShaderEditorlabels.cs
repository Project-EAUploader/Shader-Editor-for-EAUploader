using UnityEngine;
using System.IO;

public class ShaderEditorlabels
{
    private static string language;

    public static void UpdateLanguage()
    {
        language = LanguageUtility.GetCurrentLanguage();
        Initialize();
    }

    static ShaderEditorlabels()
    {
        language = LanguageUtility.GetCurrentLanguage();
        Initialize();
    }

    public static string Dialog1;
    public static string Dialog2;
    public static string OK;
    public static string EditShader;
    public static string ApplyAll;
    public static string UndoLabel;
    public static string Save;
    public static string Shaders;
    public static string Opendetail;
    public static string CloseButtonLabel;
    public static string WindowName;
    public static string WindowDescription;

    public static void Initialize()
    {
        switch (language)
        {
            case "en":
                Dialog1 = "Prefab Selection Required";
                Dialog2 = "Please select a prefab to edit shaders.";
                OK = "OK";
                EditShader = "Edit Shader";
                ApplyAll = "Apply Shader to Al";
                UndoLabel = "Undo";
                Save = "Save";
                Shaders = "Shaders";
                Opendetail = "Open Detail";
                CloseButtonLabel = "Close";
                WindowName = "Shader Editor";
                WindowDescription = "Changes the Shader of the selected avatar.\nYou can also check the available Shaders here.";
                break;
            case "ja":
                Dialog1 = "プレハブの選択が必要です";
                Dialog2 = "シェーダーを編集するプレハブを選択してください。";
                OK = "戻る";
                EditShader = "シェーダーを変更";
                ApplyAll = "全てに適用";
                UndoLabel = "変更を戻す";
                Save = "変更を保存";
                Shaders = "シェーダー一覧";
                Opendetail = "シェーダーについて";
                CloseButtonLabel = "EAUploaderに戻る";
                WindowName = "シェーダー編集";
                WindowDescription = "選択したアバターのシェーダーを変更します。\n利用可能なシェーダーはこちらで確認できます。";
                break;
        }
    }
}
