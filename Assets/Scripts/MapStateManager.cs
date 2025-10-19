using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class GameState
{
    public List<List<string>> map_data;
    public bool house_placed;
    public List<int> house_location;
    public bool buildings_placed;
    public List<List<object>> building_locations;

    public GameState()
    {
        map_data = new List<List<string>>();
        house_placed = false;
        house_location = null;
        buildings_placed = false;
        building_locations = new List<List<object>>();
    }
}

public class MapStateManager : MonoBehaviour
{
    // Configuration
    private const int MAP_SIZE = 20;
    private const string FILE_NAME = "map_state.json";
    private const string EMPTY = "0";
    private const string ROAD = "1";
    private const string HOUSE = "H";
    private const string ONLINE_SHOPPING = "W"; // warehouse
    private const string SHOPPING = "F"; // factory
    private const string EATING_OUT = "M"; // WHACKDONALD'S
    private const string NIGHT = "N"; // nightclub
    private const string GROCERIES = "T"; // A TREE

    // Prefabs
    public GameObject housePrefab;
    public GameObject warehousePrefab;
    public GameObject factoryPrefab;
    public GameObject mcdonaldsPrefab;
    public GameObject nightclubPrefab;
    public GameObject treePrefab;
    public GameObject roadPrefab;

    // List of all available non-house building types
    private static readonly string[] ALL_BUILDING_TYPES = { ONLINE_SHOPPING, SHOPPING, EATING_OUT, NIGHT, GROCERIES };

    private string GetFilePath()
    {
        return Path.Combine(Application.dataPath, FILE_NAME);
    }

    public GameState LoadGameState()
{
    string filePath = GetFilePath();

    // Default blank state
    GameState defaultState = new GameState();
    for (int i = 0; i < MAP_SIZE; i++)
    {
        List<string> row = new List<string>();
        for (int j = 0; j < MAP_SIZE; j++)
        {
            row.Add(EMPTY);
        }
        defaultState.map_data.Add(row);
    }

    if (File.Exists(filePath))
    {
        string json = File.ReadAllText(filePath);

        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.Log("Existing file is empty, reinitialising new state...");
            return defaultState;
        }

        Debug.Log($"Loading existing state from {filePath}...");
        GameState loadedState = JsonUtility.FromJson<GameState>(json);

        if (loadedState.map_data == null || loadedState.map_data.Count == 0)
        {
            Debug.Log("Loaded file has no map data, regenerating default map...");
            return defaultState;
        }

        return loadedState;
    }
    else
    {
        Debug.Log("No existing state found. Initializing new map...");
        return defaultState;
    }
}

    public void SaveGameState(GameState state)
    {
        string filePath = GetFilePath();
        string json = JsonUtility.ToJson(state, true);
        File.WriteAllText(filePath, json);
        Debug.Log($"State successfully saved to {filePath}");
    }

    public void RenderMap(GameState state)
    {
        float cellSize = 1.0f;
        for (int row = 0; row < MAP_SIZE; row++)
        {
            for (int col = 0; col < MAP_SIZE; col++)
            {
                string cell = state.map_data[row][col];
                float offsetX = -(MAP_SIZE * cellSize) / 2f;
                float offsetY = (MAP_SIZE * cellSize) / 2f;

                Vector3 position = new Vector3(
                    offsetX + col * cellSize + cellSize / 2f,
                    offsetY - row * cellSize - cellSize / 2f,
                    0
                );
                GameObject prefab = null;

                switch (cell)
                {
                    case "H": prefab = housePrefab; break;
                    case "W": prefab = warehousePrefab; break;
                    case "F": prefab = factoryPrefab; break;
                    case "M": prefab = mcdonaldsPrefab; break;
                    case "N": prefab = nightclubPrefab; break;
                    case "T": prefab = treePrefab; break;
                    case "1": prefab = roadPrefab; break;
                }

                if (prefab != null)
                {
                    GameObject obj = Instantiate(prefab, position, Quaternion.identity);
                    var info = obj.AddComponent<BuildingInfo>();
                    info.buildingType = cell;
                    info.builtDate = DateTime.Now.ToString("yyyy-MM-dd");
                    info.spentOn = GetSpentOnFromType(cell);
                    info.spentAmount = UnityEngine.Random.Range(100f, 1000f);
                }
            }
        }
    }
    string GetSpentOnFromType(string type) { 
        switch (type) { 
            case "M": return "Eating Out"; 
            case "W": return "Online Shopping"; 
            case "F": return "Shein"; 
            case "N": return "Nightlife"; 
            case "T": return "Groceries"; 
            case "S": return "Retail Shopping"; 
            case "H": return "Rent"; 
            default: return "Misc"; } }

    public GameState PlaceHouse(GameState state)
    {
        if (state.house_placed)
        {
            Debug.Log($"House already placed at [{state.house_location[0]}, {state.house_location[1]}]. Skipping placement.");
            return state;
        }

        // Determine random coordinates (0 to 9)
        int row = UnityEngine.Random.Range(0, MAP_SIZE);
        int col = UnityEngine.Random.Range(0, MAP_SIZE);

        // Update the map and state variables
        if (state.map_data[row][col] == EMPTY)
        {
            state.map_data[row][col] = HOUSE;
            state.house_placed = true;
            state.house_location = new List<int> { row, col };
        }

        Debug.Log($"House randomly placed at Row: {row}, Col: {col}");
        return state;
    }

    public (GameState, List<object>) PlaceBuildingRandom(GameState state, string buildingType)
    {
        // Repeatedly attempt to find an empty spot
        while (true)
        {
            int row = UnityEngine.Random.Range(0, MAP_SIZE);
            int col = UnityEngine.Random.Range(0, MAP_SIZE);

            // Check if the spot is EMPTY
            if (state.map_data[row][col] == EMPTY)
            {
                // Place the building and exit the loop
                state.map_data[row][col] = buildingType;
                List<object> location = new List<object> { row, col, buildingType };

                Debug.Log($"Building {buildingType} randomly placed at Row: {row}, Col: {col}");
                return (state, location);
            }
        }
    }

    public void AddBuilding(string buildingType, GameState gameState)
    {
        var (updatedState, newLocation) = PlaceBuildingRandom(gameState, buildingType);
        
        // Manually update the building_locations list and flag
        gameState.building_locations.Add(newLocation);
        gameState.buildings_placed = true;
    }

    // --- Main Logic ---
    void Start()
    {
        // 1. Load the state from the file
        GameState gameState = LoadGameState();

        // 2. Place rows of roads
        for (int i = 1; i < MAP_SIZE; i += 2)
        {
            for (int j = 0; j < MAP_SIZE; j++)
            {
                gameState.map_data[i][j] = ROAD;
            }
        }

        // 3. Run the house placement logic
        gameState = PlaceHouse(gameState);

        // 4. INITIAL BUILDING PLACEMENT: Uncomment this if you need to add a new building
        foreach (string buildingType in ALL_BUILDING_TYPES)
        {
            AddBuilding(buildingType, gameState);
            AddBuilding(buildingType, gameState);
            AddBuilding(buildingType, gameState);
            AddBuilding(buildingType, gameState);
            AddBuilding(buildingType, gameState);
            AddBuilding(buildingType, gameState);
            AddBuilding(buildingType, gameState);
            AddBuilding(buildingType, gameState);
            AddBuilding(buildingType, gameState);
            AddBuilding(buildingType, gameState);
            AddBuilding(buildingType, gameState);
            AddBuilding(buildingType, gameState);
            AddBuilding(buildingType, gameState);
            AddBuilding(buildingType, gameState);
            AddBuilding(buildingType, gameState);
            AddBuilding(buildingType, gameState);
            AddBuilding(buildingType, gameState);
            AddBuilding(buildingType, gameState);
        }

        // 5. Save the updated state permanently
        SaveGameState(gameState);

        // 6. Render the map based on the current state
        RenderMap(gameState);
        Debug.Log($"JSON content:\n{JsonUtility.ToJson(gameState, true)}");
    }
}