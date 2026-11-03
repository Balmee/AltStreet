
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public class OsmWorldImporter : MonoBehaviour
{
    [Header("World generation")]
    [SerializeField] private float groundY = 0f;
    [SerializeField] private float roadY = 0.06f;
    [SerializeField] private float buildingY = 0.02f;
    [SerializeField] private float defaultBuildingHeight = 9f;
    [SerializeField] private float heightPerLevel = 3f;

    
    [Header("Pavements")]
    [SerializeField] private float pavementWidth = 2.5f;
    [SerializeField] private float pavementHeight = 0.05f;
    [SerializeField] private float pavementY = 0.02f;

    [SerializeField] private Color pavementColor =
        new Color(0.34f, 0.33f, 0.31f);

    private Material pavementMaterial;


    [Header("Materials")]
    [SerializeField] private Color roadColor =
        new Color(0.12f, 0.13f, 0.14f);

    [SerializeField] private Color buildingColor =
        new Color(0.38f, 0.39f, 0.40f);

    [SerializeField] private Color namedBuildingColor =
        new Color(0.48f, 0.45f, 0.40f);

    [Header("Debug")]
    [SerializeField] private bool generateOnStart = true;

    private Transform generatedRoot;
    private Material roadMaterial;
    private Material buildingMaterial;
    private Material namedMaterial;

    [Serializable]
    private class OsmWorldData
    {
        public int schema_version;
        public string source;
        public string attribution;
        public OsmLocation location;
        public RoadFeature[] roads;
        public BuildingFeature[] buildings;
        public float half_size_metres;
    }

    [Serializable]
    private class OsmLocation
    {
        public string name;
        public float latitude;
        public float longitude;
    }

    [Serializable]
    private class GeoPoint
    {
        public float x;
        public float z;
    }

    [Serializable]
    private class RoadFeature
    {
        public long osm_id;
        public string highway;
        public string name;
        public GeoPoint[] coordinates;
    }

    [Serializable]
    private class BuildingFeature
    {
        public long osm_id;
        public string building;
        public string levels;
        public string name;
        public GeoPoint[] coordinates;
    }

    private void Start()
    {
        if (generateOnStart)
            GenerateWorld();
    }

    [ContextMenu("Generate OSM World")]
    public void GenerateWorld()
    {
        string path = Path.Combine(
            Application.streamingAssetsPath,
            "StreetVision",
            "osm_world.json"
        );

        if (!File.Exists(path))
        {
            Debug.LogError(
                $"STREETVISION: OSM file not found: {path}"
            );
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            OsmWorldData data =
                JsonUtility.FromJson<OsmWorldData>(json);

            if (data == null)
            {
                Debug.LogError("STREETVISION: JSON could not be parsed.");
                return;
            }

            if (data.roads == null || data.buildings == null)
            {
                Debug.LogError(
                    "STREETVISION: Missing roads or buildings arrays. " +
                    "Check the JSON schema."
                );
                return;
            }

            CreateMaterials();
            ClearGeneratedWorld();

            GameObject root = new GameObject("GeneratedOSMWorld");
            generatedRoot = root.transform;
            generatedRoot.SetParent(transform, false);

            CreateGround(data);

            int roadsCreated = 0;
            int buildingsCreated = 0;

            foreach (RoadFeature road in data.roads)
            {
                if (CreateRoad(road))
                    roadsCreated++;
            }

            foreach (BuildingFeature building in data.buildings)
            {
                if (CreateBuilding(building))
                    buildingsCreated++;
            }

            Debug.Log(
                $"STREETVISION: Imported {data.location?.name}. " +
                $"Generated {roadsCreated} roads and " +
                $"{buildingsCreated} buildings. " +
                $"Source: {data.attribution}"
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"STREETVISION: Import failed: {exception}"
            );
        }
    }

    
    private void CreateGround(OsmWorldData data)
    {
        // Cover the complete imported area with a solid floor.
        float halfSize = 180f;

        if (data != null)
        {
            // The JSON's half_size_metres field is optional in this
            // first version; use the exported value when available.
            halfSize = data.half_size_metres > 0
                ? data.half_size_metres
                : 180f;
        }

        GameObject ground = GameObject.CreatePrimitive(
            PrimitiveType.Cube
        );

        ground.name = "OSM_Ground";
        ground.transform.SetParent(generatedRoot, false);

        // Centre the ground on the geographic origin.
        ground.transform.localPosition =
            new Vector3(0f, groundY - 0.6f, 0f);

        ground.transform.localScale =
            new Vector3(halfSize * 2f, 1f, halfSize * 2f);

        Renderer renderer = ground.GetComponent<Renderer>();

        if (renderer != null)
        {
            renderer.sharedMaterial = buildingMaterial;
        }

        // CreatePrimitive adds a BoxCollider automatically.
        // The top of the cube is exactly at groundY - 0.1f.
    }


    private void CreateMaterials()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
        {
            throw new Exception("Could not find a compatible shader.");
        }

        roadMaterial = new Material(shader);
        roadMaterial.color = roadColor;

        buildingMaterial = new Material(shader);
        buildingMaterial.color = buildingColor;

        namedMaterial = new Material(shader);
        namedMaterial.color = namedBuildingColor;

        pavementMaterial = new Material(shader);
        pavementMaterial.color = pavementColor;
    }

    private void ClearGeneratedWorld()
    {
        Transform existing = transform.Find("GeneratedOSMWorld");

        if (existing != null)
        {
            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }
    }

    private bool CreateRoad(RoadFeature road)
    {
        if (road?.coordinates == null ||
            road.coordinates.Length < 2)
            return false;

        List<Vector3> points = new List<Vector3>();

        foreach (GeoPoint point in road.coordinates)
        {
            if (point == null)
                continue;

            Vector3 position = new Vector3(
                point.x, roadY, point.z
            );

            if (points.Count == 0 ||
                Vector3.Distance(points[points.Count - 1], position) > 0.01f)
            {
                points.Add(position);
            }
        }

        if (points.Count < 2)
            return false;

        float width = GetRoadWidth(road.highway);
        float halfWidth = width * 0.5f;

        int count = points.Count;

        Vector3[] vertices = new Vector3[count * 2];
        int[] triangles = new int[(count - 1) * 6];

        for (int i = 0; i < count; i++)
        {
            Vector3 direction;

            if (i == 0)
            {
                direction = points[1] - points[0];
            }
            else if (i == count - 1)
            {
                direction = points[i] - points[i - 1];
            }
            else
            {
                Vector3 incoming =
                    (points[i] - points[i - 1]).normalized;

                Vector3 outgoing =
                    (points[i + 1] - points[i]).normalized;

                direction = incoming + outgoing;

                if (direction.sqrMagnitude < 0.001f)
                    direction = outgoing;
            }

            direction.y = 0f;
            direction.Normalize();

            Vector3 perpendicular = new Vector3(
                -direction.z, 0f, direction.x
            );

            float scale = halfWidth;

            // Compensate for the angle at bends.
            if (i > 0 && i < count - 1)
            {
                Vector3 incoming =
                    (points[i] - points[i - 1]).normalized;

                Vector3 outgoing =
                    (points[i + 1] - points[i]).normalized;

                Vector3 incomingSide =
                    new Vector3(-incoming.z, 0f, incoming.x);

                float denominator =
                    Vector3.Dot(perpendicular, incomingSide);

                if (Mathf.Abs(denominator) > 0.25f)
                    scale = Mathf.Min(
                        halfWidth / Mathf.Abs(denominator),
                        halfWidth * 2f
                    );
            }

            Vector3 offset = perpendicular * scale;

            vertices[i * 2] = points[i] + offset;
            vertices[i * 2 + 1] = points[i] - offset;
        }

        for (int i = 0; i < count - 1; i++)
        {
            int vertex = i * 2;
            int triangle = i * 6;

            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 2;
            triangles[triangle + 2] = vertex + 1;

            triangles[triangle + 3] = vertex + 1;
            triangles[triangle + 4] = vertex + 2;
            triangles[triangle + 5] = vertex + 3;
        }

        Mesh mesh = new Mesh();
        mesh.name = $"OSM_Road_{road.osm_id}";

        mesh.vertices = vertices;
        mesh.triangles = triangles;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject roadObject = new GameObject(
            $"Road_{road.osm_id}_{road.highway}"
        );

        roadObject.transform.SetParent(generatedRoot, false);

        MeshFilter filter = roadObject.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer =
            roadObject.AddComponent<MeshRenderer>();

        renderer.sharedMaterial = roadMaterial;

        MeshCollider collider =
            roadObject.AddComponent<MeshCollider>();

        collider.sharedMesh = mesh;

        CreatePavementsForRoad(
            points,
            width,
            $"Pavement_{road.osm_id}"
        );

        return true;
    }

    private float GetRoadWidth(string highway)
    {
        switch (highway)
        {
            case "motorway":
            case "trunk":
                return 10f;

            case "primary":
                return 8f;

            case "secondary":
                return 7f;

            case "tertiary":
                return 6f;

            case "residential":
            case "unclassified":
                return 4.5f;

            case "service":
                return 3f;

            case "footway":
            case "pedestrian":
            case "path":
                return 1.8f;

            default:
                return 4f;
        }
    }



        
    private void CreatePavementsForRoad(
        List<Vector3> points,
        float roadWidth,
        string name)
    {
        if (points == null || points.Count < 2)
            return;

        int count = points.Count;

        Vector3[] leftInner = new Vector3[count];
        Vector3[] leftOuter = new Vector3[count];

        Vector3[] rightInner = new Vector3[count];
        Vector3[] rightOuter = new Vector3[count];

        float halfRoad = roadWidth * 0.5f;

        for (int i = 0; i < count; i++)
        {
            Vector3 direction;

            if (i == 0)
                direction = points[1] - points[0];
            else if (i == count - 1)
                direction = points[i] - points[i - 1];
            else
            {
                Vector3 incoming =
                    (points[i] - points[i - 1]).normalized;

                Vector3 outgoing =
                    (points[i + 1] - points[i]).normalized;

                direction = incoming + outgoing;

                if (direction.sqrMagnitude < 0.001f)
                    direction = outgoing;
            }

            direction.y = 0f;
            direction.Normalize();

            Vector3 side = new Vector3(
                -direction.z, 0f, direction.x
            );

            float scale = halfRoad;

            if (i > 0 && i < count - 1)
            {
                Vector3 incoming =
                    (points[i] - points[i - 1]).normalized;

                Vector3 incomingSide = new Vector3(
                    -incoming.z, 0f, incoming.x
                );

                float denominator =
                    Vector3.Dot(side, incomingSide);

                if (Mathf.Abs(denominator) > 0.25f)
                    scale = Mathf.Min(
                        halfRoad / Mathf.Abs(denominator),
                        halfRoad * 2f
                    );
            }

            Vector3 innerOffset = side * scale;
            Vector3 outerOffset =
                side * (scale + pavementWidth);

            Vector3 point = points[i];

            leftInner[i] = new Vector3(
                point.x + innerOffset.x,
                pavementY + pavementHeight,
                point.z + innerOffset.z
            );

            leftOuter[i] = new Vector3(
                point.x + outerOffset.x,
                pavementY + pavementHeight,
                point.z + outerOffset.z
            );

            rightInner[i] = new Vector3(
                point.x - innerOffset.x,
                pavementY + pavementHeight,
                point.z - innerOffset.z
            );

            rightOuter[i] = new Vector3(
                point.x - outerOffset.x,
                pavementY + pavementHeight,
                point.z - outerOffset.z
            );
        }

        CreatePavementMesh(
            leftInner,
            leftOuter,
            $"{name}_Left"
        );

        CreatePavementMesh(
            rightInner,
            rightOuter,
            $"{name}_Right"
        );
    }

    private void CreatePavementMesh(
        Vector3[] inner,
        Vector3[] outer,
        string objectName)
    {
        int count = inner.Length;

        Vector3[] vertices = new Vector3[count * 2];
        int[] triangles = new int[(count - 1) * 6];

        for (int i = 0; i < count; i++)
        {
            vertices[i * 2] = inner[i];
            vertices[i * 2 + 1] = outer[i];
        }

        for (int i = 0; i < count - 1; i++)
        {
            int vertex = i * 2;
            int triangle = i * 6;

            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 2;
            triangles[triangle + 2] = vertex + 1;

            triangles[triangle + 3] = vertex + 1;
            triangles[triangle + 4] = vertex + 2;
            triangles[triangle + 5] = vertex + 3;
        }

        Mesh mesh = new Mesh();
        mesh.name = objectName;

        mesh.vertices = vertices;
        mesh.triangles = triangles;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject pavement = new GameObject(objectName);
        pavement.transform.SetParent(generatedRoot, false);

        MeshFilter filter = pavement.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer =
            pavement.AddComponent<MeshRenderer>();

        renderer.sharedMaterial = pavementMaterial;

        MeshCollider collider =
            pavement.AddComponent<MeshCollider>();

        collider.sharedMesh = mesh;
    }


    private bool CreateBuilding(BuildingFeature building)
    {
        if (building?.coordinates == null ||
            building.coordinates.Length < 3)
            return false;

        List<Vector2> footprint = new List<Vector2>();

        foreach (GeoPoint point in building.coordinates)
        {
            if (point == null)
                continue;

            Vector2 p = new Vector2(point.x, point.z);

            if (footprint.Count == 0 ||
                Vector2.Distance(footprint[footprint.Count - 1], p) > 0.01f)
            {
                footprint.Add(p);
            }
        }

        // Remove a repeated closing point if present.
        if (footprint.Count > 2 &&
            Vector2.Distance(footprint[0], footprint[footprint.Count - 1]) < 0.01f)
        {
            footprint.RemoveAt(footprint.Count - 1);
        }

        if (footprint.Count < 3)
            return false;

        List<int> roofTriangles = Triangulate(footprint);

        if (roofTriangles.Count < 3)
        {
            Debug.LogWarning(
                $"Could not triangulate building {building.osm_id}"
            );
            return false;
        }

        float height = GetBuildingHeight(building);

        int count = footprint.Count;
        List<Vector3> vertices = new List<Vector3>(count * 2);

        // Bottom ring followed by top ring.
        for (int i = 0; i < count; i++)
        {
            vertices.Add(new Vector3(
                footprint[i].x, buildingY, footprint[i].y
            ));
        }

        for (int i = 0; i < count; i++)
        {
            vertices.Add(new Vector3(
                footprint[i].x, buildingY + height, footprint[i].y
            ));
        }

        List<int> triangles = new List<int>();

        float area = SignedArea(footprint);

        // Roof triangles need to face upwards in Unity.
        for (int i = 0; i < roofTriangles.Count; i += 3)
        {
            int a = roofTriangles[i];
            int b = roofTriangles[i + 1];
            int c = roofTriangles[i + 2];

            if (area > 0f)
            {
                triangles.Add(count + a);
                triangles.Add(count + c);
                triangles.Add(count + b);
            }
            else
            {
                triangles.Add(count + a);
                triangles.Add(count + b);
                triangles.Add(count + c);
            }
        }

        // Extrude each footprint edge into a wall.
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;

            int bottomA = i;
            int bottomB = next;
            int topA = count + i;
            int topB = count + next;

            if (area > 0f)
            {
                triangles.Add(bottomA);
                triangles.Add(topA);
                triangles.Add(topB);

                triangles.Add(bottomA);
                triangles.Add(topB);
                triangles.Add(bottomB);
            }
            else
            {
                triangles.Add(bottomA);
                triangles.Add(topB);
                triangles.Add(topA);

                triangles.Add(bottomA);
                triangles.Add(bottomB);
                triangles.Add(topB);
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = $"OSM_Building_{building.osm_id}";

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        string displayName = string.IsNullOrWhiteSpace(building.name)
            ? $"Building_{building.osm_id}"
            : building.name;

        GameObject buildingObject = new GameObject(displayName);
        buildingObject.transform.SetParent(generatedRoot, false);

        MeshFilter filter = buildingObject.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer =
            buildingObject.AddComponent<MeshRenderer>();

        renderer.sharedMaterial =
            string.IsNullOrWhiteSpace(building.name)
                ? buildingMaterial
                : namedMaterial;

        MeshCollider collider =
            buildingObject.AddComponent<MeshCollider>();

        collider.sharedMesh = mesh;

        return true;
    }

    private float GetBuildingHeight(BuildingFeature building)
    {
        if (!string.IsNullOrWhiteSpace(building.levels) &&
            float.TryParse(
                building.levels,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float levels
            ) &&
            levels > 0f)
        {
            return Mathf.Clamp(levels * heightPerLevel, 3f, 100f);
        }

        switch (building.building)
        {
            case "church":
            case "cathedral":
                return 18f;

            case "commercial":
            case "office":
                return 15f;

            case "industrial":
                return 8f;

            case "house":
            case "detached":
            case "semidetached_house":
                return 7f;

            default:
                return defaultBuildingHeight;
        }
    }

    private float SignedArea(List<Vector2> polygon)
    {
        float area = 0f;

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Count];

            area += a.x * b.y - b.x * a.y;
        }

        return area * 0.5f;
    }

    // Ear-clipping triangulation supports many concave footprints.
    private List<int> Triangulate(List<Vector2> polygon)
    {
        List<int> result = new List<int>();
        List<int> remaining = new List<int>();

        for (int i = 0; i < polygon.Count; i++)
            remaining.Add(i);

        float area = SignedArea(polygon);

        if (Mathf.Abs(area) < 0.001f)
            return result;

        int orientation = area > 0f ? 1 : -1;
        int safety = polygon.Count * polygon.Count;

        while (remaining.Count > 3 && safety-- > 0)
        {
            bool foundEar = false;

            for (int i = 0; i < remaining.Count; i++)
            {
                int previous = remaining[
                    (i - 1 + remaining.Count) % remaining.Count
                ];

                int current = remaining[i];

                int next = remaining[
                    (i + 1) % remaining.Count
                ];

                Vector2 a = polygon[previous];
                Vector2 b = polygon[current];
                Vector2 c = polygon[next];

                float cross =
                    (b.x - a.x) * (c.y - a.y) -
                    (b.y - a.y) * (c.x - a.x);

                if (cross * orientation <= 0.0001f)
                    continue;

                bool containsPoint = false;

                foreach (int candidate in remaining)
                {
                    if (candidate == previous ||
                        candidate == current ||
                        candidate == next)
                        continue;

                    if (PointInTriangle(
                        polygon[candidate], a, b, c))
                    {
                        containsPoint = true;
                        break;
                    }
                }

                if (containsPoint)
                    continue;

                result.Add(previous);
                result.Add(current);
                result.Add(next);

                remaining.RemoveAt(i);
                foundEar = true;
                break;
            }

            if (!foundEar)
                return new List<int>();
        }

        if (remaining.Count == 3)
        {
            result.Add(remaining[0]);
            result.Add(remaining[1]);
            result.Add(remaining[2]);
        }

        return result;
    }

    private bool PointInTriangle(
        Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p, a, b);
        float d2 = Cross(p, b, c);
        float d3 = Cross(p, c, a);

        bool hasNegative = d1 < -0.0001f ||
                           d2 < -0.0001f ||
                           d3 < -0.0001f;

        bool hasPositive = d1 > 0.0001f ||
                           d2 > 0.0001f ||
                           d3 > 0.0001f;

        return !(hasNegative && hasPositive);
    }

    private float Cross(Vector2 p, Vector2 a, Vector2 b)
    {
        return (p.x - b.x) * (a.y - b.y) -
               (a.x - b.x) * (p.y - b.y);
    }

    private void OnDestroy()
    {
        if (roadMaterial != null)
            Destroy(roadMaterial);

        if (buildingMaterial != null)
            Destroy(buildingMaterial);

        if (namedMaterial != null)
            Destroy(namedMaterial);

        if (pavementMaterial != null)
            Destroy(pavementMaterial);
    }
}
