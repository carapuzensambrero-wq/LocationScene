using UnityEngine;
using System.IO;
using System.Linq;

public class TerrainSplatmapAssignerSimple : MonoBehaviour
{
    [Header("Настройки")]
    [SerializeField] private string splatmapsFolderPath = "Assets/SplatTextures";
    [SerializeField] private int splatCount = 3;

    [Header("Настройки слоев")]
    [SerializeField] private Texture2D[] layerTextures;
    [SerializeField] private string[] layerNames = { "Layer 1", "Layer 2", "Layer 3" };
    [SerializeField] private float[] layerTiles = { 15f, 15f, 15f };

    [Header("Настройки разрешения")]
    [SerializeField] private int targetResolution = 1025;

    [Header("Инверсия карты")]
    [SerializeField] private bool flipX = false; // Инвертировать по горизонтали
    [SerializeField] private bool flipY = true; // Инвертировать по вертикали (по умолчанию true для Gaea)

    void Start()
    {
        AssignSplatmaps();
    }

    [ContextMenu("Assign Splatmaps")]
    public void AssignSplatmaps()
    {
        Debug.Log("=== ПРИСВОЕНИЕ SPLATMAP ===");
        Debug.Log($"Flip X: {flipX}, Flip Y: {flipY}");

        if (!Directory.Exists(splatmapsFolderPath))
        {
            Debug.LogError($"Папка не найдена: {splatmapsFolderPath}");
            return;
        }

        // Получаем все PNG файлы
        DirectoryInfo dir = new DirectoryInfo(splatmapsFolderPath);
        FileInfo[] imageFiles = dir.GetFiles("*.png")
            .OrderBy(f => f.Name)
            .ToArray();

        Debug.Log($"Найдено PNG файлов: {imageFiles.Length}");

        if (imageFiles.Length == 0)
        {
            Debug.LogError($"В папке {splatmapsFolderPath} нет PNG файлов!");
            return;
        }

        // Получаем террейны как в HeightMap
        Terrain[] terrains = GetComponentsInChildren<Terrain>();
        Debug.Log($"Найдено террейнов: {terrains.Length}");

        // Сортируем по индексу в иерархии
        terrains = terrains.OrderBy(t => t.transform.GetSiblingIndex()).ToArray();

        // Выводим соответствие
        Debug.Log("=== СООТВЕТСТВИЕ ТЕРРЕЙНОВ И ФАЙЛОВ ===");
        int count = Mathf.Min(terrains.Length, imageFiles.Length);
        for (int i = 0; i < count; i++)
        {
            Debug.Log($"[{i}] {terrains[i].name} -> {imageFiles[i].Name}");
        }

        // Настраиваем слои для всех террейнов
        Debug.Log("=== НАСТРОЙКА СЛОЕВ ===");
        for (int i = 0; i < terrains.Length; i++)
        {
            SetupTerrainLayers(terrains[i]);
        }

        // Устанавливаем разрешение
        Debug.Log($"=== УСТАНОВКА РАЗРЕШЕНИЯ {targetResolution} ===");
        for (int i = 0; i < terrains.Length; i++)
        {
            TerrainData data = terrains[i].terrainData;
            if (data.alphamapResolution != targetResolution)
            {
                data.alphamapResolution = targetResolution;
            }
        }

        int assignedCount = 0;

        for (int i = 0; i < terrains.Length && i < imageFiles.Length; i++)
        {
            Terrain terrain = terrains[i];
            FileInfo imageFile = imageFiles[i];

            Debug.Log($"Обработка [{i}]: {terrain.name} <- {imageFile.Name}");

            bool success = SetTerrainSplatmap(terrain, imageFile.FullName);

            if (success)
            {
                assignedCount++;
                Debug.Log($"✓ [{i}] {terrain.name} получил SplatMap");
            }
            else
            {
                Debug.LogError($"✗ [{i}] Не удалось установить SplatMap для {terrain.name}");
            }
        }

        Debug.Log($"=== ГОТОВО: Установлено {assignedCount} SplatMap ===");
    }

    private void SetupTerrainLayers(Terrain terrain)
    {
        TerrainData terrainData = terrain.terrainData;

        if (terrainData.terrainLayers != null && terrainData.terrainLayers.Length == splatCount)
        {
            return;
        }

        TerrainLayer[] layers = new TerrainLayer[splatCount];

        for (int i = 0; i < splatCount; i++)
        {
            TerrainLayer layer = new TerrainLayer();
            layer.name = layerNames.Length > i ? layerNames[i] : $"Layer {i + 1}";
            layer.tileSize = new Vector2(layerTiles.Length > i ? layerTiles[i] : 15f,
                                         layerTiles.Length > i ? layerTiles[i] : 15f);

            if (layerTextures != null && i < layerTextures.Length && layerTextures[i] != null)
            {
                layer.diffuseTexture = layerTextures[i];
            }
            else
            {
                layer.diffuseTexture = CreateDummyTexture(i);
            }

            layers[i] = layer;
        }

        terrainData.terrainLayers = layers;
    }

    private Texture2D CreateDummyTexture(int index)
    {
        Texture2D texture = new Texture2D(64, 64);
        Color[] colors = new Color[64 * 64];

        Color[] layerColors = {
            new Color(0.2f, 0.6f, 0.2f),
            new Color(0.5f, 0.3f, 0.1f),
            new Color(0.3f, 0.3f, 0.3f),
            new Color(0.8f, 0.7f, 0.5f)
        };

        Color color = index < layerColors.Length ? layerColors[index] : Color.gray;

        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = color;
        }

        texture.SetPixels(colors);
        texture.Apply();

        return texture;
    }

    private bool SetTerrainSplatmap(Terrain terrain, string filePath)
    {
        try
        {
            byte[] fileData = File.ReadAllBytes(filePath);

            Texture2D texture = new Texture2D(2, 2);
            if (!texture.LoadImage(fileData))
            {
                Debug.LogError($"Не удалось загрузить: {Path.GetFileName(filePath)}");
                return false;
            }

            TerrainData terrainData = terrain.terrainData;

            if (terrainData.terrainLayers == null || terrainData.terrainLayers.Length == 0)
            {
                Debug.LogError($"Нет настроенных слоев для {terrain.name}!");
                return false;
            }

            int layers = terrainData.terrainLayers.Length;

            // Приводим к целевому разрешению
            if (texture.width != targetResolution || texture.height != targetResolution)
            {
                texture = ResizeTexture(texture, targetResolution, targetResolution);
            }

            // Получаем пиксели
            Color[] pixels = texture.GetPixels();
            int width = texture.width;
            int height = texture.height;

            // ===== ПРИМЕНЯЕМ ИНВЕРСИЮ =====
            if (flipX || flipY)
            {
                Debug.Log($"Применяем инверсию: FlipX={flipX}, FlipY={flipY}");
                pixels = FlipPixels(pixels, width, height);
            }

            // Устанавливаем разрешение SplatMap
            if (terrainData.alphamapResolution != targetResolution)
            {
                terrainData.alphamapResolution = targetResolution;
            }

            // Создаем SplatMap
            float[,,] splatmapData = new float[height, width, layers];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    Color pixel = pixels[index];

                    splatmapData[y, x, 0] = pixel.r;

                    if (layers > 1)
                        splatmapData[y, x, 1] = pixel.g;

                    if (layers > 2)
                        splatmapData[y, x, 2] = pixel.b;

                    if (layers > 3)
                        splatmapData[y, x, 3] = pixel.a;

                    for (int l = 4; l < layers; l++)
                    {
                        splatmapData[y, x, l] = 0;
                    }
                }
            }

            NormalizeSplatmap(splatmapData, width, height, layers);
            terrainData.SetAlphamaps(0, 0, splatmapData);
            terrain.Flush();

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка для {terrain.name}: {e.Message}");
            return false;
        }
    }

    // ===== МЕТОД ДЛЯ ИНВЕРСИИ ПИКСЕЛЕЙ =====
    private Color[] FlipPixels(Color[] pixels, int width, int height)
    {
        Color[] flipped = new Color[pixels.Length];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int srcX = flipX ? (width - 1 - x) : x;
                int srcY = flipY ? (height - 1 - y) : y;

                int srcIndex = srcY * width + srcX;
                int dstIndex = y * width + x;

                flipped[dstIndex] = pixels[srcIndex];
            }
        }

        return flipped;
    }

    private Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
    {
        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
        rt.filterMode = FilterMode.Bilinear;

        Graphics.Blit(source, rt);

        Texture2D result = new Texture2D(newWidth, newHeight);
        RenderTexture.active = rt;
        result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        result.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        return result;
    }

    private void NormalizeSplatmap(float[,,] splatmapData, int width, int height, int layers)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float sum = 0;
                for (int layer = 0; layer < layers; layer++)
                {
                    sum += splatmapData[y, x, layer];
                }

                if (sum > 0.01f)
                {
                    for (int layer = 0; layer < layers; layer++)
                    {
                        splatmapData[y, x, layer] /= sum;
                    }
                }
                else
                {
                    splatmapData[y, x, 0] = 1;
                    for (int layer = 1; layer < layers; layer++)
                    {
                        splatmapData[y, x, layer] = 0;
                    }
                }
            }
        }
    }

    [ContextMenu("Clear All Splatmaps")]
    private void ClearAllSplatmaps()
    {
        Debug.Log("=== ОЧИСТКА ВСЕХ SPLATMAP ===");

        Terrain[] terrains = GetComponentsInChildren<Terrain>()
            .OrderBy(t => t.transform.GetSiblingIndex())
            .ToArray();

        foreach (Terrain terrain in terrains)
        {
            TerrainData data = terrain.terrainData;

            if (data.terrainLayers == null || data.terrainLayers.Length == 0)
                continue;

            int layers = data.terrainLayers.Length;
            int resolution = data.alphamapResolution;

            float[,,] emptySplatmap = new float[resolution, resolution, layers];

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    emptySplatmap[y, x, 0] = 1;
                    for (int layer = 1; layer < layers; layer++)
                    {
                        emptySplatmap[y, x, layer] = 0;
                    }
                }
            }

            data.SetAlphamaps(0, 0, emptySplatmap);
            terrain.Flush();
        }

        Debug.Log("Очистка завершена!");
    }

    [ContextMenu("Show Mapping")]
    private void ShowMapping()
    {
        Debug.Log("=== СООТВЕТСТВИЕ ТЕРРЕЙНОВ И ФАЙЛОВ ===");

        if (!Directory.Exists(splatmapsFolderPath))
        {
            Debug.LogError($"Папка не найдена: {splatmapsFolderPath}");
            return;
        }

        DirectoryInfo dir = new DirectoryInfo(splatmapsFolderPath);
        FileInfo[] imageFiles = dir.GetFiles("*.png")
            .OrderBy(f => f.Name)
            .ToArray();

        Terrain[] terrains = GetComponentsInChildren<Terrain>()
            .OrderBy(t => t.transform.GetSiblingIndex())
            .ToArray();

        Debug.Log($"Террейнов: {terrains.Length}, Файлов: {imageFiles.Length}");
        Debug.Log($"Flip X: {flipX}, Flip Y: {flipY}");
        Debug.Log("");

        int count = Mathf.Min(terrains.Length, imageFiles.Length);
        for (int i = 0; i < count; i++)
        {
            Debug.Log($"[{i}] {terrains[i].name} -> {imageFiles[i].Name}");
        }

        if (terrains.Length != imageFiles.Length)
        {
            Debug.LogWarning($"Несоответствие! Террейнов: {terrains.Length}, Файлов: {imageFiles.Length}");
        }
    }

    [ContextMenu("Toggle Flip X")]
    private void ToggleFlipX()
    {
        flipX = !flipX;
        Debug.Log($"Flip X: {(flipX ? "ВКЛ" : "ВЫКЛ")}");
    }

    [ContextMenu("Toggle Flip Y")]
    private void ToggleFlipY()
    {
        flipY = !flipY;
        Debug.Log($"Flip Y: {(flipY ? "ВКЛ" : "ВЫКЛ")}");
    }

    [ContextMenu("Set 1025 Resolution")]
    private void Set1025Resolution()
    {
        targetResolution = 1025;
        Debug.Log("Установлено разрешение 1025");
    }

    [ContextMenu("Set 2048 Resolution")]
    private void Set2048Resolution()
    {
        targetResolution = 2048;
        Debug.Log("Установлено разрешение 2048");
    }

    [ContextMenu("Set RGB Mode (3 layers)")]
    private void SetRGBMode()
    {
        splatCount = 3;
        layerNames = new string[] { "Grass", "Rock", "Dirt" };
        layerTiles = new float[] { 15f, 15f, 15f };
        Debug.Log("Установлен режим RGB (3 слоя)");
    }

    [ContextMenu("Set RGBA Mode (4 layers)")]
    private void SetRGBAMode()
    {
        splatCount = 4;
        layerNames = new string[] { "Grass", "Rock", "Dirt", "Sand" };
        layerTiles = new float[] { 15f, 15f, 15f, 15f };
        Debug.Log("Установлен режим RGBA (4 слоя)");
    }

    [ContextMenu("Set Flip Settings for Gaea")]
    private void SetGaeaFlipSettings()
    {
        flipX = false;
        flipY = true;
        Debug.Log("Установлены настройки для Gaea: FlipX=false, FlipY=true");
    }

    [ContextMenu("Disable All Flips")]
    private void DisableAllFlips()
    {
        flipX = false;
        flipY = false;
        Debug.Log("Все инверсии отключены");
    }
}