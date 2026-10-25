using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EchoesOfGenesis.Editor
{
    public static class P09MaterialConverter
    {
        private const string LoRShaderName = "EchoesOfGenesis/LoR_Character_URP";
        private const string GenshinShaderName = "EchoesOfGenesis/Genshin_Toon_URP";
        private const string TexturesRoot = "Assets/P09_Modular_Humanoid/Model_DATA/Textures";
        private const string MatCapRoot = "Assets/P09_Modular_Humanoid/Model_DATA/Textures/MatCap";

        [MenuItem("Tools/Echoes of Genesis/Convert P09 to Genshin Toon Style (Recommended)")]
        public static void ConvertToGenshinToon()
        {
            ConvertMaterials(GenshinShaderName, true);
        }

        [MenuItem("Tools/Echoes of Genesis/Convert P09 to LoR Stylized Style")]
        public static void ConvertToLoRStylized()
        {
            ConvertMaterials(LoRShaderName, false);
        }

        private static void ConvertMaterials(string shaderName, bool isGenshin)
        {
            Shader targetShader = Shader.Find(shaderName);
            if (targetShader == null)
            {
                string relativePath = isGenshin 
                    ? "Assets/EchoesOfGenesis/Shaders/Genshin_Toon_URP.shader" 
                    : "Assets/EchoesOfGenesis/Shaders/LoR_Character_URP.shader";
                targetShader = AssetDatabase.LoadAssetAtPath<Shader>(relativePath);
            }

            if (targetShader == null)
            {
                string searchPattern = isGenshin ? "Genshin_Toon_URP t:Shader" : "LoR_Character_URP t:Shader";
                string[] shaderGuids = AssetDatabase.FindAssets(searchPattern);
                if (shaderGuids != null && shaderGuids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(shaderGuids[0]);
                    targetShader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                }
            }

            if (targetShader == null)
            {
                Debug.LogError($"[Toon Converter] Could not find shader '{shaderName}'.");
                return;
            }

            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/P09_Modular_Humanoid/Model_DATA/Materials" });
            int convertedCount = 0;

            Debug.Log($"[Toon Converter] Loaded shader '{targetShader.name}'. Converting {materialGuids.Length} materials to {(isGenshin ? "Genshin Anime Toon" : "LoR")}...");

            Texture2D metalMatCap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{MatCapRoot}/metal_10.png") 
                                  ?? AssetDatabase.LoadAssetAtPath<Texture2D>($"{MatCapRoot}/P03_Metal.png");
            Texture2D skinMatCap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{MatCapRoot}/skinmatcap_p05.png");
            Texture2D eyeMatCap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{MatCapRoot}/eyepupilmatcap02.png");

            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                if (mat.isVariant) continue;

                Undo.RecordObject(mat, "Convert Character Shader");
                mat.shader = targetShader;

                string matName = mat.name;
                bool isSkin = matName.Contains("Skin");
                bool isArmor = matName.Contains("Armor");
                bool isUnderwear = matName.Contains("UnderWear");
                bool isHair = matName.Contains("Hair");
                bool isEye = matName.Contains("Eye");
                bool isWeapon = matName.Contains("Weapon");

                AssignTexturesForMaterial(mat, matName, isSkin, isArmor, isUnderwear, isHair, isEye, isWeapon, metalMatCap, skinMatCap, eyeMatCap);

                if (isGenshin)
                {
                    ApplyGenshinPreset(mat, isSkin, isArmor, isUnderwear, isHair, isEye, isWeapon);
                }
                else
                {
                    ApplyLoRPreset(mat, isSkin, isArmor, isUnderwear, isHair, isEye, isWeapon);
                }

                EditorUtility.SetDirty(mat);
                convertedCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SceneView.RepaintAll();
            Debug.Log($"[Toon Converter] Successfully converted {convertedCount} materials to {(isGenshin ? "Genshin Anime Toon" : "LoR")} style!");
        }

        private static void AssignTexturesForMaterial(
            Material mat, string matName,
            bool isSkin, bool isArmor, bool isUnderwear, bool isHair, bool isEye, bool isWeapon,
            Texture2D metalMatCap, Texture2D skinMatCap, Texture2D eyeMatCap)
        {
            if (mat.GetTexture("_BaseMap") == null && mat.mainTexture != null)
            {
                mat.SetTexture("_BaseMap", mat.mainTexture);
            }

            if (isSkin)
            {
                bool isBright = matName.Contains("Bright");
                bool isDark = matName.Contains("Dark");
                bool isFemale = matName.Contains("Female");
                string bodyFolder = isFemale ? "Female_Body" : "Male_Body";
                string prefix = isFemale ? "P09_Female_Body" : "P09_Male_Body";

                string diffName = isBright ? $"{prefix}_Bright_Diff.png" : (isDark ? $"{prefix}_Dark_Diff.png" : $"{prefix}_Diff.png");
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesRoot}/{bodyFolder}/{diffName}"));
                mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesRoot}/{bodyFolder}/{prefix}_Normal.png"));
                mat.SetTexture("_RimMask", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesRoot}/{bodyFolder}/{prefix}_RimMask.png"));
            }
            else if (isArmor && matName.Contains("003"))
            {
                bool isFemale = matName.Contains("Female");
                string armorFolder = isFemale ? "Armors/Fem_Armor_003" : "Armors/Male_Armor_003";
                string prefix = isFemale ? "P09_Female_Armor_003" : "P09_Male_Armor_003";

                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesRoot}/{armorFolder}/{prefix}_Diff.png"));
                mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesRoot}/{armorFolder}/{prefix}_Normal.png"));
                mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesRoot}/{armorFolder}/{prefix}_Metal.png"));
                mat.SetTexture("_RimMask", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesRoot}/{armorFolder}/{prefix}_Rim.png"));
                mat.SetTexture("_MatCapTex", metalMatCap);
            }
            else if (isUnderwear)
            {
                bool isFemale = matName.Contains("Female");
                string underFolder = isFemale ? "Armors/Fem_UnderWear" : "Armors/Male_UnderWear";
                string prefix = isFemale ? "P09_Female_UnderWear" : "P09_Male_UnderWear";

                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesRoot}/{underFolder}/{prefix}_Diff.png"));
                mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesRoot}/{underFolder}/{prefix}_Normal.png"));
                mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesRoot}/{underFolder}/{prefix}_Metal.png"));
                mat.SetTexture("_RimMask", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesRoot}/{underFolder}/{prefix}_Rim.png"));
            }
            else if (isHair)
            {
                string[] parts = matName.Split('_');
                string colorName = parts[parts.Length - 1];
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesRoot}/Common/Hair/P09_Hair_{colorName}.png"));
                mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesRoot}/Common/Hair/P09_Hair_Metal.png"));
                mat.SetTexture("_RimMask", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesRoot}/Common/Hair/P09_Hair_RimMask.png"));
            }
            else if (isEye)
            {
                string[] parts = matName.Split('_');
                string colorName = parts[parts.Length - 1];
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesRoot}/Common/Eye/P09_Eye_Pupile_{colorName}.png"));
            }
            else if (isWeapon && (matName.Contains("Sword_02") || matName.Contains("Shield_02")))
            {
                bool isSword = matName.Contains("Sword");
                string weaponFolder = isSword ? "Weapons/Sword/Sword_02" : "Weapons/Shield/Shield_02";
                string prefix = isSword ? "P09_Weapon_Sword_02" : "P09_Weapon_Shield_02";

                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesRoot}/{weaponFolder}/{prefix}_Diff.png"));
                mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesRoot}/{weaponFolder}/{prefix}_Normal.png"));
                mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesRoot}/{weaponFolder}/{prefix}_Metal.png"));
                mat.SetTexture("_RimMask", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesRoot}/{weaponFolder}/{prefix}_Rim.png"));
                mat.SetTexture("_MatCapTex", metalMatCap);
            }
        }

        private static void ApplyGenshinPreset(Material mat, bool isSkin, bool isArmor, bool isUnderwear, bool isHair, bool isEye, bool isWeapon)
        {
            mat.SetColor("_BaseColor", Color.white);

            if (isSkin)
            {
                mat.SetColor("_ShadeColor", new Color(0.95f, 0.78f, 0.74f, 1.0f)); // Warm peachy anime shadow
                mat.SetFloat("_IsFace", 1.0f); // Clean anime face (no muddy nose/cheek shadow!)
                mat.SetFloat("_CelStep", 0.5f);
                mat.SetFloat("_CelFeather", 0.05f);
                mat.SetFloat("_Metallic", 0.0f);
                mat.SetFloat("_Smoothness", 0.1f);
                mat.SetFloat("_SpecIntensity", 0.1f);
                mat.SetFloat("_MatCapIntensity", 0.0f);
                mat.SetFloat("_RimIntensity", 0.3f);
                mat.SetFloat("_EnableOutline", 1.0f);
                mat.SetFloat("_OutlineWidth", 0.002f);
                mat.SetColor("_OutlineColor", new Color(0.38f, 0.22f, 0.22f, 1.0f)); // Soft brown outline
            }
            else if (isArmor || isWeapon)
            {
                mat.SetColor("_ShadeColor", new Color(0.6f, 0.56f, 0.7f, 1.0f)); // Crisp anime cel shadow
                mat.SetFloat("_IsFace", 0.0f);
                mat.SetFloat("_CelStep", 0.45f);
                mat.SetFloat("_CelFeather", 0.03f);
                mat.SetFloat("_Metallic", 0.85f);
                mat.SetFloat("_Smoothness", 0.8f);
                mat.SetFloat("_SpecIntensity", 1.5f);
                mat.SetFloat("_SpecSize", 0.08f);
                mat.SetFloat("_MatCapIntensity", 1.0f);
                mat.SetColor("_MatCapColor", new Color(1.0f, 0.9f, 0.7f, 1.0f));
                mat.SetFloat("_RimIntensity", 1.0f);
                mat.SetFloat("_RimWidth", 0.12f);
                mat.SetFloat("_EnableOutline", 1.0f);
                mat.SetFloat("_OutlineWidth", 0.0028f);
                mat.SetColor("_OutlineColor", new Color(0.12f, 0.08f, 0.15f, 1.0f)); // Crisp dark outline
            }
            else if (isHair)
            {
                mat.SetColor("_ShadeColor", new Color(0.7f, 0.62f, 0.78f, 1.0f));
                mat.SetFloat("_IsFace", 0.0f);
                mat.SetFloat("_CelStep", 0.45f);
                mat.SetFloat("_CelFeather", 0.04f);
                mat.SetFloat("_Metallic", 0.1f);
                mat.SetFloat("_Smoothness", 0.85f);
                mat.SetFloat("_SpecIntensity", 1.8f);
                mat.SetFloat("_SpecSize", 0.15f); // Hair highlight ring
                mat.SetFloat("_MatCapIntensity", 0.0f);
                mat.SetFloat("_RimIntensity", 1.0f);
                mat.SetFloat("_EnableOutline", 1.0f);
                mat.SetFloat("_OutlineWidth", 0.0025f);
                mat.SetColor("_OutlineColor", new Color(0.18f, 0.12f, 0.2f, 1.0f));
            }
            else if (isEye)
            {
                mat.SetFloat("_IsFace", 1.0f);
                mat.SetFloat("_Metallic", 0.0f);
                mat.SetFloat("_Smoothness", 0.9f);
                mat.SetFloat("_SpecIntensity", 2.0f);
                mat.SetFloat("_EnableOutline", 0.0f);
                mat.SetFloat("_RimIntensity", 0.0f);
            }
            else // Underwear, cloth
            {
                mat.SetColor("_ShadeColor", new Color(0.68f, 0.65f, 0.75f, 1.0f));
                mat.SetFloat("_IsFace", 0.0f);
                mat.SetFloat("_CelStep", 0.5f);
                mat.SetFloat("_CelFeather", 0.04f);
                mat.SetFloat("_Metallic", 0.0f);
                mat.SetFloat("_Smoothness", 0.15f);
                mat.SetFloat("_SpecIntensity", 0.2f);
                mat.SetFloat("_MatCapIntensity", 0.0f);
                mat.SetFloat("_RimIntensity", 0.5f);
                mat.SetFloat("_EnableOutline", 1.0f);
                mat.SetFloat("_OutlineWidth", 0.0025f);
                mat.SetColor("_OutlineColor", new Color(0.15f, 0.12f, 0.18f, 1.0f));
            }
        }

        private static void ApplyLoRPreset(Material mat, bool isSkin, bool isArmor, bool isUnderwear, bool isHair, bool isEye, bool isWeapon)
        {
            mat.SetColor("_BaseColor", Color.white);
            if (isSkin)
            {
                mat.SetColor("_ShadowTint", new Color(0.85f, 0.65f, 0.6f, 1.0f));
                mat.SetFloat("_ShadowThreshold", -0.05f);
                mat.SetFloat("_ShadowSoftness", 0.45f);
                mat.SetFloat("_Metallic", 0.0f);
                mat.SetFloat("_Smoothness", 0.3f);
                mat.SetFloat("_SpecularIntensity", 0.3f);
                mat.SetFloat("_MatCapIntensity", 0.0f);
                mat.SetColor("_RimColor", new Color(1.0f, 0.9f, 0.8f, 1.0f));
                mat.SetFloat("_RimPower", 4.0f);
                mat.SetFloat("_RimIntensity", 0.5f);
            }
            else if (isArmor || isWeapon)
            {
                mat.SetColor("_ShadowTint", new Color(0.4f, 0.38f, 0.5f, 1.0f));
                mat.SetFloat("_ShadowThreshold", 0.05f);
                mat.SetFloat("_ShadowSoftness", 0.28f);
                mat.SetFloat("_Metallic", 0.85f);
                mat.SetFloat("_Smoothness", 0.7f);
                mat.SetFloat("_SpecularIntensity", 1.4f);
                mat.SetFloat("_MatCapIntensity", 0.8f);
                mat.SetColor("_RimColor", new Color(1.0f, 0.85f, 0.5f, 1.0f));
                mat.SetFloat("_RimPower", 3.0f);
                mat.SetFloat("_RimIntensity", 1.2f);
            }
            else if (isHair)
            {
                mat.SetColor("_ShadowTint", new Color(0.45f, 0.4f, 0.52f, 1.0f));
                mat.SetFloat("_Metallic", 0.1f);
                mat.SetFloat("_Smoothness", 0.6f);
                mat.SetFloat("_SpecularIntensity", 1.2f);
                mat.SetFloat("_MatCapIntensity", 0.2f);
                mat.SetFloat("_RimIntensity", 1.0f);
            }
            else
            {
                mat.SetColor("_ShadowTint", new Color(0.45f, 0.42f, 0.52f, 1.0f));
                mat.SetFloat("_Metallic", 0.0f);
                mat.SetFloat("_Smoothness", 0.2f);
                mat.SetFloat("_SpecularIntensity", 0.2f);
                mat.SetFloat("_MatCapIntensity", 0.0f);
                mat.SetFloat("_RimIntensity", 0.5f);
            }
        }
    }
}
