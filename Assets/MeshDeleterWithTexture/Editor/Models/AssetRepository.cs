﻿using System.IO;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture.Models
{
    public static class AssetRepository
    {
        private const string MESHDELETER_ASSETS_FOLDER_NAME = "MDwT";
        private static readonly string MESHDELETER_COMPUTESHADER_FOLDER_PATH = Path.Combine(MESHDELETER_ASSETS_FOLDER_NAME, "ComputeShaders");
        private static readonly string MESHDELETER_LANGUAGE_PACK_FOLDER_PATH = Path.Combine(MESHDELETER_ASSETS_FOLDER_NAME, "Lang");

        public static Material LoadTextureEditMaterial()
            => new Material(Shader.Find("Unlit/TextureEdit"));

        public static ComputeShader LoadDrawComputeShader()
            => Resources.Load<ComputeShader>(Path.Combine(MESHDELETER_COMPUTESHADER_FOLDER_PATH, "colorchecker2"));

        public static ComputeShader LoadCreateUVMapComputeShader()
            => Resources.Load<ComputeShader>(Path.Combine(MESHDELETER_COMPUTESHADER_FOLDER_PATH, "getUVMap"));

        public static ComputeShader LoadCalculateSelectAreaComputeShader()
            => Resources.Load<ComputeShader>(Path.Combine(MESHDELETER_COMPUTESHADER_FOLDER_PATH, "calculateSelectArea"));

        public static LanguagePack[] LoadLanguagePacks()
            => new LanguagePack[] {
                Resources.Load<LanguagePack>(Path.Combine(MESHDELETER_LANGUAGE_PACK_FOLDER_PATH, "EN")),
                Resources.Load<LanguagePack>(Path.Combine(MESHDELETER_LANGUAGE_PACK_FOLDER_PATH, "JA"))
            };
    }
}
