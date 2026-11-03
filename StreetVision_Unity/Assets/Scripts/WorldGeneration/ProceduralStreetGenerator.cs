
using UnityEngine;

public class ProceduralStreetGenerator : MonoBehaviour
{
    [Header("Street Dimensions")]
    [Min(10f)] public float streetLength = 80f;
    [Min(4f)] public float roadWidth = 8f;
    [Min(1f)] public float pavementWidth = 3f;

    [Header("Buildings")]
    [Min(1)] public int buildingsPerSide = 8;
    [Min(5f)] public float minimumBuildingHeight = 6f;
    [Min(5f)] public float maximumBuildingHeight = 20f;
    [Min(4f)] public float buildingFrontage = 7f;
    [Min(4f)] public float buildingDepth = 9f;

    [Header("Reproducibility")]
    public int randomSeed = 42;

    private Transform generatedRoot;

    private Material roadMaterial;
    private Material pavementMaterial;
    private Material markingMaterial;
    private Material[] buildingMaterials;

    private void Start()
    {
        GenerateWorld();
    }

    [ContextMenu("Generate World")]
    public void GenerateWorld()
    {
        ClearGeneratedWorld();
        CreateMaterials();

        GameObject root = new GameObject("GeneratedWorld");
        root.transform.SetParent(transform, false);
        generatedRoot = root.transform;

        CreateGround();
        CreateRoad();
        CreatePavements();
        CreateRoadMarkings();
        CreateBuildings();

        Debug.Log(
            $"STREETVISION generated a street with " +
            $"{buildingsPerSide * 2} buildings."
        );
    }

    private void CreateMaterials()
    {
        roadMaterial = CreateMaterial(
            "Road Asphalt", new Color(0.19f, 0.20f, 0.22f)
        );

        pavementMaterial = CreateMaterial(
            "Pavement Concrete", new Color(0.43f, 0.44f, 0.43f)
        );

        markingMaterial = CreateMaterial(
            "Road Markings", new Color(0.85f, 0.82f, 0.69f)
        );

        buildingMaterials = new Material[]
        {
            CreateMaterial("Building Sandstone", new Color(0.55f, 0.48f, 0.39f)),
            CreateMaterial("Building Brick", new Color(0.43f, 0.25f, 0.21f)),
            CreateMaterial("Building Concrete", new Color(0.48f, 0.49f, 0.47f)),
            CreateMaterial("Building Pale Stone", new Color(0.67f, 0.63f, 0.54f)),
            CreateMaterial("Building Dark Brick", new Color(0.31f, 0.30f, 0.29f))
        };
    }

    private Material CreateMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.name = materialName;
        material.color = color;
        return material;
    }

    private void CreateGround()
    {
        CreateBox(
            "Ground",
            new Vector3(0f, -0.25f, streetLength * 0.5f),
            new Vector3(roadWidth + pavementWidth * 2f + 45f,
                        0.5f, streetLength + 20f),
            CreateMaterial("Ground Base", new Color(0.22f, 0.27f, 0.22f))
        );
    }

    private void CreateRoad()
    {
        CreateBox(
            "Road",
            new Vector3(0f, 0.01f, streetLength * 0.5f),
            new Vector3(roadWidth, 0.1f, streetLength),
            roadMaterial
        );
    }

    private void CreatePavements()
    {
        float x = roadWidth * 0.5f + pavementWidth * 0.5f;

        CreateBox(
            "Left Pavement",
            new Vector3(-x, 0.12f, streetLength * 0.5f),
            new Vector3(pavementWidth, 0.2f, streetLength),
            pavementMaterial
        );

        CreateBox(
            "Right Pavement",
            new Vector3(x, 0.12f, streetLength * 0.5f),
            new Vector3(pavementWidth, 0.2f, streetLength),
            pavementMaterial
        );
    }

    private void CreateRoadMarkings()
    {
        float z = 2f;
        int index = 0;

        while (z < streetLength - 2f)
        {
            CreateBox(
                $"Centre Line {index}",
                new Vector3(0f, 0.075f, z),
                new Vector3(0.12f, 0.025f, 2.2f),
                markingMaterial
            );

            z += 4.5f;
            index++;
        }
    }

    private void CreateBuildings()
    {
        Random.State previousState = Random.state;
        Random.InitState(randomSeed);

        float spacing = streetLength / buildingsPerSide;
        float firstZ = spacing * 0.5f;

        float buildingX =
            roadWidth * 0.5f + pavementWidth + buildingDepth * 0.5f;

        for (int i = 0; i < buildingsPerSide; i++)
        {
            float z = firstZ + i * spacing;
            float height = Random.Range(
                minimumBuildingHeight,
                maximumBuildingHeight
            );

            float frontage = buildingFrontage *
                Random.Range(0.85f, 1.15f);

            Material material = buildingMaterials[
                Random.Range(0, buildingMaterials.Length)
            ];

            CreateBuilding(
                $"Left Building {i + 1}",
                new Vector3(-buildingX, 0f, z),
                new Vector3(buildingDepth, height, frontage),
                material
            );

            float oppositeHeight = Random.Range(
                minimumBuildingHeight,
                maximumBuildingHeight
            );

            Material oppositeMaterial = buildingMaterials[
                Random.Range(0, buildingMaterials.Length)
            ];

            CreateBuilding(
                $"Right Building {i + 1}",
                new Vector3(buildingX, 0f, z),
                new Vector3(buildingDepth, oppositeHeight, frontage),
                oppositeMaterial
            );
        }

        Random.state = previousState;
    }

    private void CreateBuilding(
        string objectName,
        Vector3 position,
        Vector3 size,
        Material material)
    {
        // Cubes have their pivot at the centre, so raise them by half
        // their height to rest on the ground.
        position.y = size.y * 0.5f;

        CreateBox(objectName, position, size, material);
    }

    private GameObject CreateBox(
        string objectName,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = objectName;
        box.transform.SetParent(generatedRoot, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = localScale;

        Renderer renderer = box.GetComponent<Renderer>();
        renderer.sharedMaterial = material;

        return box;
    }

    private void ClearGeneratedWorld()
    {
        Transform oldRoot = transform.Find("GeneratedWorld");

        if (oldRoot != null)
        {
            if (Application.isPlaying)
                Destroy(oldRoot.gameObject);
            else
                DestroyImmediate(oldRoot.gameObject);
        }
    }
}
