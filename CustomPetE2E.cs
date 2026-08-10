using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace WorkMatePro
{
    internal static class CustomPetE2E
    {
        public static int PrepareFixture()
        {
            string root = Environment.GetEnvironmentVariable("WORKMATE_TEST_DIR");
            string enabled = Environment.GetEnvironmentVariable("WORKMATE_CUSTOM_PET_E2E");
            if (enabled != "1" || string.IsNullOrWhiteSpace(root)) return 12;
            try
            {
                root = Path.GetFullPath(root);
                Directory.CreateDirectory(root);
                CustomPetService service = new CustomPetService(root);
                string reference = Path.Combine(root, "custom-pet-e2e-reference.png");
                SavePng(PetAssets.Get("01-cat", "idle"), reference);
                CustomPetResult project = service.CreateProject("团子 E2E", "测试三花猫", new[] { reference });
                if (!project.Success) throw new InvalidDataException(project.Error);

                string sources = Path.Combine(root, "custom-pet-e2e-sources");
                Directory.CreateDirectory(sources);
                Dictionary<string, string> poses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string action in CustomPetService.RequiredActions)
                {
                    string path = Path.Combine(sources, "任意文件名-" + action + ".png");
                    SavePng(PetAssets.Get("01-cat", "idle"), path);
                    poses[action] = path;
                }
                CustomPetResult prepared = service.PrepareGeneratedAssets(project.ProjectDirectory, poses);
                if (!prepared.Success) throw new InvalidDataException(prepared.Error);

                File.WriteAllText(Path.Combine(root, "custom-pet-e2e-project.txt"), project.ProjectDirectory, new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(root, "custom-pet-e2e-fixture.log"),
                    "PASS fixture\nproject=" + project.ProjectDirectory + "\nassets=" + prepared.Validation.CheckedAssets,
                    new UTF8Encoding(false));
                return 0;
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(Path.Combine(root ?? Environment.CurrentDirectory, "custom-pet-e2e-fixture.log"), "FAIL\n" + ex, new UTF8Encoding(false)); }
                catch { }
                return 13;
            }
        }

        private static void SavePng(BitmapSource source, string path)
        {
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }
    }
}
