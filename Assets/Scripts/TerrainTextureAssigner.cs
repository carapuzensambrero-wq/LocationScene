using UnityEngine;
using System.IO;
using System.Linq;

public class TerrainTextureAssigner : MonoBehaviour
{
    [Header("Настройки")]
    [SerializeField] private string texturesFolderPath = "Assets/HeightTextures";

    [Header("Настройки RAW")]
    [SerializeField] private int rawWidth = 1025;
    [SerializeField] private int rawHeight = 1025;
    [SerializeField] private bool flipY = false; // Для переворота по Y если нужно

    void Start()
    {
        AssignHeightMaps();
    }

    [ContextMenu("Assign HeightMaps")]
    public void AssignHeightMaps()
    {
        Debug.Log("=== НАЧАЛО ПРИСВОЕНИЯ HEIGHTMAP ===");

        // Проверяем папку
        if (!Directory.Exists(texturesFolderPath))
        {
            Debug.LogError($"ПАПКА НЕ НАЙДЕНА: {texturesFolderPath}");
            return;
        }

        // Получаем все RAW файлы
        DirectoryInfo dir = new DirectoryInfo(texturesFolderPath);
        FileInfo[] rawFiles = dir.GetFiles("*.raw")
            .OrderBy(f => f.Name)
            .ToArray();

        Debug.Log($"Найдено RAW файлов: {rawFiles.Length}");

        if (rawFiles.Length == 0)
        {
            Debug.LogError("В папке нет .raw файлов!");
            return;
        }

        // Получаем все террейны в порядке иерархии
        Terrain[] terrains = GetComponentsInChildren<Terrain>();
        Debug.Log($"Найдено террейнов: {terrains.Length}");

        // Сортируем по порядку в иерархии (по индексу)
        terrains = terrains.OrderBy(t => t.transform.GetSiblingIndex()).ToArray();

        int assignedCount = 0;

        for (int i = 0; i < terrains.Length && i < rawFiles.Length; i++)
        {
            Terrain terrain = terrains[i];
            FileInfo rawFile = rawFiles[i];

            Debug.Log($"Обработка [{i}]: {terrain.name} <- {rawFile.Name}");

            // Загружаем и устанавливаем HeightMap
            bool success = SetTerrainHeightMap(terrain, rawFile.FullName);

            if (success)
            {
                assignedCount++;
                Debug.Log($"✓ {terrain.name} получил HeightMap из {rawFile.Name}");
            }
            else
            {
                Debug.LogError($"✗ Не удалось установить HeightMap для {terrain.name}");
            }
        }

        Debug.Log($"=== ГОТОВО: Установлено {assignedCount} HeightMap из {terrains.Length} террейнов ===");
    }

    private bool SetTerrainHeightMap(Terrain terrain, string filePath)
    {
        try
        {
            // Читаем RAW файл
            byte[] rawData = File.ReadAllBytes(filePath);

            // Проверяем размер
            int expectedSize = rawWidth * rawHeight * 2; // 16-bit = 2 байта на пиксель

            if (rawData.Length != expectedSize)
            {
                Debug.LogWarning($"Размер не совпадает! Ожидается: {expectedSize}, получено: {rawData.Length}");
                // Пробуем определить размер
                int possibleSize = (int)Mathf.Sqrt(rawData.Length / 2);
                if (possibleSize * possibleSize * 2 == rawData.Length)
                {
                    rawWidth = possibleSize;
                    rawHeight = possibleSize;
                    Debug.Log($"Автоопределен размер: {rawWidth}x{rawHeight}");
                }
                else
                {
                    Debug.LogError($"Не удалось определить размер для {Path.GetFileName(filePath)}");
                    return false;
                }
            }

            // Получаем TerrainData
            TerrainData terrainData = terrain.terrainData;

            // Изменяем размер HeightMap если нужно
            if (terrainData.heightmapResolution != rawWidth)
            {
                Debug.Log($"Изменение разрешения HeightMap с {terrainData.heightmapResolution} на {rawWidth}");
                terrainData.heightmapResolution = rawWidth;
            }

            // Создаем массив высот
            float[,] heights = new float[rawHeight, rawWidth];

            // Заполняем массив из RAW данных
            for (int y = 0; y < rawHeight; y++)
            {
                for (int x = 0; x < rawWidth; x++)
                {
                    int index = (y * rawWidth + x) * 2;

                    if (index + 1 < rawData.Length)
                    {
                        // Читаем 16-bit значение (Little Endian)
                        ushort value = (ushort)(rawData[index] | (rawData[index + 1] << 8));
                        // Нормализуем в диапазон 0-1
                        heights[y, x] = value / 65535.0f;
                    }
                    else
                    {
                        heights[y, x] = 0;
                    }
                }
            }

            // Переворачиваем по Y если нужно
            if (flipY)
            {
                float[,] flipped = new float[rawHeight, rawWidth];
                for (int y = 0; y < rawHeight; y++)
                {
                    for (int x = 0; x < rawWidth; x++)
                    {
                        flipped[y, x] = heights[rawHeight - 1 - y, x];
                    }
                }
                heights = flipped;
            }

            // Устанавливаем HeightMap
            terrainData.SetHeights(0, 0, heights);

            // Обновляем террейн
            terrain.Flush();

            Debug.Log($"HeightMap установлен: {rawWidth}x{rawHeight}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка установки HeightMap для {terrain.name}: {e.Message}");
            return false;
        }
    }

    // Метод для установки текстуры как HeightMap (если у вас есть обычные изображения)
    private bool SetTerrainHeightMapFromTexture(Terrain terrain, string filePath)
    {
        try
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2);

            if (!texture.LoadImage(fileData))
            {
                Debug.LogError($"Не удалось загрузить текстуру: {Path.GetFileName(filePath)}");
                return false;
            }

            TerrainData terrainData = terrain.terrainData;

            // Изменяем разрешение
            int resolution = texture.width;
            if (terrainData.heightmapResolution != resolution)
            {
                terrainData.heightmapResolution = resolution;
            }

            // Конвертируем текстуру в HeightMap
            float[,] heights = new float[resolution, resolution];
            Color[] pixels = texture.GetPixels();

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int index = y * resolution + x;
                    // Берем среднее значение каналов (или можно взять красный канал)
                    heights[y, x] = pixels[index].grayscale;
                }
            }

            terrainData.SetHeights(0, 0, heights);
            terrain.Flush();

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка: {e.Message}");
            return false;
        }
    }

    [ContextMenu("Reset All Terrains")]
    private void ResetAllTerrains()
    {
        Terrain[] terrains = GetComponentsInChildren<Terrain>();
        foreach (Terrain terrain in terrains)
        {
            TerrainData data = terrain.terrainData;
            int res = data.heightmapResolution;
            float[,] flat = new float[res, res];
            data.SetHeights(0, 0, flat);
            terrain.Flush();
        }
        Debug.Log($"Сброшено {terrains.Length} террейнов");
    }

    [ContextMenu("Check Path")]
    private void CheckPath()
    {
        Debug.Log($"Путь: {texturesFolderPath}");
        Debug.Log($"Полный путь: {Path.GetFullPath(texturesFolderPath)}");
        Debug.Log($"Существует: {Directory.Exists(texturesFolderPath)}");

        if (Directory.Exists(texturesFolderPath))
        {
            var files = Directory.GetFiles(texturesFolderPath, "*.raw");
            Debug.Log($"RAW файлов: {files.Length}");
            foreach (var file in files)
            {
                var info = new FileInfo(file);
                Debug.Log($"- {info.Name} ({info.Length} байт)");
            }
        }
    }

    [ContextMenu("Set Gaea 1025 Settings")]
    private void SetGaeaSettings()
    {
        rawWidth = 1025;
        rawHeight = 1025;
        flipY = false;
        Debug.Log("Установлены настройки для Gaea (1025x1025)");
    }
}