using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
//using UnityEditor;

namespace ProceduralGenerator
{

    // Building class is used to store information about the building such as space allocation and its size
    [System.Serializable]
    public class Building
    {
        public GameObject buildingObject;
        public Vector2Int spaceAllocation = new Vector2Int(1, 1);
        [HideInInspector] public Vector3 size;
        [HideInInspector] public bool allocateOnly = false;
        public bool ruralBuilding = false;

        
        // This is used to calculate the size and correct space allocation
        public void calculateSize()
        {
            if (spaceAllocation.sqrMagnitude < 1)
            {
                spaceAllocation = new Vector2Int(1, 1);
            }

            size = buildingObject.GetComponent<Renderer>().bounds.size;
        }
    }

    [System.Serializable]
    public class RoadAttributes
    {
        private string name = "Tier";
        public Texture texture;
        public float roadWidth;

    }

    public class CityGenerator : MonoBehaviour
    {
        


        // Building variables
        [Header("Building Settings")]
        [SerializeField] private Building defaultCommercial;
        [SerializeField] private List<Building> commercialBuildings = new List<Building>();
        [SerializeField] private Building defaultResidential;
        [SerializeField] private List<Building> residentialBuildings = new List<Building>();
        [SerializeField] private int buildingLayer = 10;
        [SerializeField] [Range(1, 5)] private int numberOfBuildingTiers = 1;


        // Road variables
        [Header ("Road Settings")]
        [SerializeField] private List<RoadAttributes> roadTier = new List<RoadAttributes>();
        [SerializeField] private GameObject roads;
        [SerializeField] private int roadLayer = 8;
        [SerializeField] private int roadSpacing = 3;

        // Spawning variables
        [Header("Road Generation Settings")]
        [SerializeField] private Vector2 habitableMapArea = new Vector2(50, 50);
        [SerializeField] private int maximumRoadUnitLength = 50;
        [SerializeField] private int minimumRoadUnitLength = 8;
        [SerializeField] private int seedNumber = 7;
        [SerializeField] private bool autoGenerate = false;

        [ShowIf("autoGenerate", 0, ShowIfAttribute.Comparison.Not)]
        [SerializeField] private int maximumDepth = 100;
        [ShowIf("autoGenerate", 0, ShowIfAttribute.Comparison.Not)]
        [SerializeField] private int maximumNoOfRoads = 10000;
        [ShowIf("autoGenerate", 0, ShowIfAttribute.Comparison.Not)]
        [Tooltip("Density value between 0.001 and 1")]
        [SerializeField] private float density = 0.05f;


        // Building Instantiation variables
        [Header("Building Instantiation Settings")]
        [Tooltip("Randomisation without pattern")]
        [SerializeField] private bool totallyRandom = false;
        [Tooltip("This defines the minimum lot allocation of a building which is also considered as the unit length of the generator")]
        [SerializeField] private int buildingLotSize = 15;
        [SerializeField] private float noiseScale = 70.0f;
        [SerializeField] private Vector2 offset = new Vector2(0,0);


        // Population Density variables
        [Header("Population Density Settings")]
        [SerializeField] private float overallNoiseScale = 70.0f;
        [SerializeField] private Vector2 overallOffset = new Vector2(0, 0);


        // Variables used for camera switching
        [Header("Camera Settings")]
        [SerializeField] private GameObject overheadCamera = null;
        [SerializeField] private GameObject firstPersonCamera = null;
        [Tooltip("Camera to use when VR device is connected")]
        [SerializeField] private GameObject cameraRig = null;


        // Not necessary ones or previous generation
        [Header("Previous Implementation")]
        [SerializeField] private bool completeGrid = false;
        [ShowIf("completeGrid", 0, ShowIfAttribute.Comparison.Equals)]
        [SerializeField] private Terrain landMap;
        [ShowIf("completeGrid", 0, ShowIfAttribute.Comparison.Equals)]
        [SerializeField] private int roadGridSpacing = 3;
        [ShowIf("completeGrid", 0, ShowIfAttribute.Comparison.Equals)]
        [SerializeField] private GameObject border;
        [ShowIf("completeGrid", 0, ShowIfAttribute.Comparison.Equals)]
        [SerializeField] private float terrainHeightLimit = 50;
        [ShowIf("completeGrid", 0, ShowIfAttribute.Comparison.Equals)]
        [SerializeField] private float steepnessLimit = 20;
        [ShowIf("completeGrid", 0, ShowIfAttribute.Comparison.Equals)]
        [SerializeField] private float roadHeightLimit = 50;
        [ShowIf("completeGrid", 0, ShowIfAttribute.Comparison.Equals)]
        [SerializeField] private float roadsteepnessLimit = 20;




        private int buildingMaxSize;
        private int buildingMinSize;
        private float roadWidth;
        private int numberOfRoads;
        private int instantiatedRoad = 0;
        private int destroyedRoad = 0;
        private bool steamVRConnected = false;

        private List<Vector3> mutatePosition = new List<Vector3>();

        private GameObject m_marker;

        private float actualDensity = 0.05f;


        // Use this for initialization
        void Start()
        {
            // Checks if Steam VR is connected and turns on camera if it is
            if (XRDevice.isPresent && cameraRig != null)
            {
                cameraRig.SetActive(true);
                firstPersonCamera.SetActive(false);
                steamVRConnected = true;
            }
            else if (firstPersonCamera != null && overheadCamera != null)
            {
                // Turns the first person camera on and overhead camera off when no steam vr connected
                firstPersonCamera.SetActive(true);
                overheadCamera.SetActive(false);
            }

// This part is used to show error dialog messages if there are parts of the city generator that have not been initialised properly
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying)
            {
                if (defaultResidential.buildingObject == null)
                {
                    UnityEditor.EditorUtility.DisplayDialog("Error", "No default residential building", "Exit");
                    UnityEditor.EditorApplication.isPlaying = false;
                }
                if (defaultCommercial.buildingObject == null)
                {
                    UnityEditor.EditorUtility.DisplayDialog("Error", "No default residential building", "Exit");
                    UnityEditor.EditorApplication.isPlaying = false;
                }
                if (residentialBuildings.Count == 0)
                {
                    UnityEditor.EditorUtility.DisplayDialog("Error", "Residential building list is empty", "Exit");
                    UnityEditor.EditorApplication.isPlaying = false;
                }
                if (commercialBuildings.Count == 0)
                {
                    UnityEditor.EditorUtility.DisplayDialog("Error", "Commercial building list is empty", "Exit");
                    UnityEditor.EditorApplication.isPlaying = false;
                }
                if (completeGrid && landMap == null)
                {
                    UnityEditor.EditorUtility.DisplayDialog("Error", "No Terrain to use for the complete grid setup", "Exit");
                    UnityEditor.EditorApplication.isPlaying = false;
                }
            }

#endif
            // This automatically quits the game when ran and it is not programmed properly since it can cause null error
            if (defaultResidential.buildingObject == null || defaultCommercial.buildingObject == null ||
                residentialBuildings.Count == 0 || commercialBuildings.Count == 0 || (completeGrid && landMap == null))
            {
                Application.Quit();
            }

            // Density correction
            if (density > 1)
                density = 1;


            if ( (commercialBuildings.Count + residentialBuildings.Count) / (float)numberOfBuildingTiers < 1)
            {
                numberOfBuildingTiers = (commercialBuildings.Count + residentialBuildings.Count);
            }

            if (roadSpacing < 3)
            {
                roadSpacing = 3;
            }

            // This foreach loop is used to go through each commercial building and checking if a building object is allocated then calculates the size
            // If no building object is allocated, default building will be allocated
            foreach (Building building in commercialBuildings)
            {
                if (building.buildingObject == null)
                {
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.DisplayDialog("Warning", "An element of commercial building does not have a building object. \n Default commercial building will be used.", "OK");
#endif
                    building.buildingObject = defaultCommercial.buildingObject;
                }
                building.calculateSize();
            }

            // This foreach loop is used to go through each residential building and checking if a building object is allocated then calculates the size
            // If no building object is allocated, default building will be allocated
            foreach (Building building in residentialBuildings)
            {
                if (building.buildingObject == null)
                {
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.DisplayDialog("Warning", "An element of residential building does not have a building object. \n Default residential building will be used.", "OK");
#endif
                    building.buildingObject = defaultResidential.buildingObject;
                }
                building.calculateSize();
            }

            // This is used to initialise the state
            UnityEngine.Random.InitState(seedNumber);

            // Used to stretch out the density of the lower end since it has better spread
            actualDensity = (Mathf.Pow(density, 2));

            // Instantiator of road
            m_marker = new GameObject();

            // Uses the size of the road as its width
            roadWidth = roads.GetComponent<Renderer>().bounds.size.x;
            Vector3 cityCentre = new Vector3(habitableMapArea.x / 2, 0, habitableMapArea.y / 2);

            // Ability to change between the previous implementation and the new one
            if (completeGrid)
            {
                if (!landMap.gameObject.activeSelf)
                    landMap.gameObject.SetActive(true);
                CompleteGrid();
            }
            else
                MainRoad(cityCentre);

        }

        // This is first city procedural generation where the city is completely grid based, this is adapted from a youtube tutorial but has terrain integrated into the generation
        private void CompleteGrid()
        {

            int gridMapX = (int)(habitableMapArea.x / buildingLotSize);
            int gridMapZ = (int)(habitableMapArea.y / buildingLotSize);
            int[,] buildingNumberArray = new int[gridMapX, gridMapZ];


            for (int x = 0; x < gridMapX; x++)
            {
                for (int z = 0; z < gridMapZ; z++)
                {
                    buildingNumberArray[x, z] = (int)(Mathf.PerlinNoise(x / noiseScale + offset.x, z / noiseScale + offset.y) * commercialBuildings.Count) % commercialBuildings.Count;
                    if ((x % roadGridSpacing == 0 || z % roadGridSpacing == 0) && buildingNumberArray[x, z] != 0)
                    {
                        buildingNumberArray[x, z] = -1;
                    }

                }
            }

            // This might involve conversion issues
            for (int x = 0; x < gridMapX; x++)
            {
                for (int z = 0; z < gridMapZ; z++)
                {
                    int buildingNumber = buildingNumberArray[x, z];
                    float y = Terrain.activeTerrain.SampleHeight(transform.position + new Vector3(x * buildingLotSize, 0, z * buildingLotSize));
                    Vector3 position;
                    if (buildingNumber < 0)
                    {
                        // This is for road generation
                        position = transform.position + new Vector3(x * buildingLotSize, 0.01f + y, z * buildingLotSize);
                    }
                    else
                    {
                        // This is position correction for buildings
                        position = transform.position + new Vector3(x * buildingLotSize, commercialBuildings[buildingNumber].size.y / 2.0f + y, z * buildingLotSize);
                    }

                    // This normalise the position to terrain coordinate and then use that information to check the steepness at that point
                    float normalizedX = (position.x - Terrain.activeTerrain.GetPosition().x) / Terrain.activeTerrain.terrainData.size.x;
                    float normalizedY = (position.z - Terrain.activeTerrain.GetPosition().z) / Terrain.activeTerrain.terrainData.size.z;
                    float steepness = Terrain.activeTerrain.terrainData.GetSteepness(normalizedX, normalizedY);

                    // This part instantiate the building depending on whether it is within the steepness and terrain height limit
                    if (buildingNumber > 0 && steepness <= steepnessLimit && y <= terrainHeightLimit)
                    {
                        GameObject clone = Instantiate(commercialBuildings[buildingNumber].buildingObject, position, Quaternion.identity);
                    }
                    else if (buildingNumber == -1 && steepness <= roadsteepnessLimit && y <= roadHeightLimit)
                    {
                        Vector3 terrainNormal = Terrain.activeTerrain.terrainData.GetInterpolatedNormal(normalizedX, normalizedY);
                        GameObject clone = Instantiate(roads, position, Quaternion.identity);
                        clone.transform.up = terrainNormal;
                    }
                }
            }
        }


        // This creates the first and main road of the city generator where the rest of the road network branches off from
        // cityCentre is the location of the city centre where the main road will spawn
        private void MainRoad(Vector3 cityCentre)
        {
            int gridMapX = (int)(habitableMapArea.x / buildingLotSize);
            int gridMapZ = (int)(habitableMapArea.y / buildingLotSize);

            // Length of the main road
            int length = gridMapZ * buildingLotSize;
            Debug.Log("Length: " + length);

            // Reference to the start and end of the road
            Vector3 mainStartNode = (transform.position + cityCentre + new Vector3(0, 0.01f, 0));
            Vector3 mainEndNode = (mainStartNode + transform.forward * length);

            mainStartNode -= new Vector3(0, 0, length / 2);
            mainEndNode -= new Vector3(0, 0, length / 2);

            // This instantiate the main road and use the transformation rotation of the parent city generator
            GameObject clone = Instantiate(roads, mainStartNode, transform.rotation);
            clone.transform.localScale = Vector3.Scale(clone.transform.localScale, new Vector3(1, 1, length/roadWidth));

            // Put this into road layer
            clone.layer = 8;

            // This makes sure that it translate the whole object by half the length you transform it
            clone.transform.position = mainStartNode + new Vector3(0, 0, length / 2);


            numberOfRoads = 1;

            // Add Road component to the main road
            clone.AddComponent<Road>();

            // Allocate number of possible nodes using building lot size as a spacing
            int newNumberOfNodes = (int)((mainEndNode - mainStartNode).magnitude / (buildingLotSize * roadSpacing));
            int numberOfSpace = newNumberOfNodes * roadSpacing;

            int depth = 0;

            // Number of actual branches that will be populated according to density
            int numBranches = (int)Math.Ceiling(newNumberOfNodes * 2 * actualDensity);

            // Creates the branches depending on the density
            for (int mainBranch = 0; mainBranch < numBranches; mainBranch++)
            {
                int nodeNumber = UnityEngine.Random.Range(1, newNumberOfNodes);

                CreateRoad(mainStartNode, mainEndNode, transform.rotation, clone, nodeNumber, newNumberOfNodes, depth);
            }

            SetBuildingSpace(numberOfSpace, mainStartNode, clone);

        }

        // This creates the roads the branches off from the parent road and requires the following
        // StartNode and EndNode which are vector position where the main road starts and ends
        // rotation which is a quaternion rotation of the parent road
        // previousRoad actual reference to the parent road
        // nodeNumber which is the initial random position where to instantiate the new road
        // numberOfNodes which is the number of nodes that the parent node can handle
        // depth refers to the iteration depth of the road from the main road
        private void CreateRoad(Vector3 startNode, Vector3 endNode, Quaternion rotation, GameObject previousRoad, int nodeNumber, int numberOfNodes, int depth)
        {
            depth++;

            // Instantiate the road
            m_marker.transform.position = startNode;
            m_marker.transform.rotation = rotation;

            bool left = false;
            bool right = false;

            // Randomly choose which direction to turn
            int randomNumber = UnityEngine.Random.Range(0, 2) % 2;
            if (randomNumber == 1)
            {

                m_marker.transform.Rotate(0, 90, 0);
                left = true;
            }
            else
            {
                m_marker.transform.Rotate(0, -90, 0);
                right = true;
            }
            int turn = Convert.ToInt32(left) | (Convert.ToInt32(right) << 1);

            // Check if another road is on this position
            Dictionary<int, int> reference = previousRoad.GetComponent<Road>().occupiedNode;
            int numberOfIterations = 0;

            // Checks where can the road be instantiated without overlapping with other roads
            while (reference.ContainsKey(nodeNumber) && nodeNumber < numberOfNodes && numberOfIterations < 3)
            {
                int roadDirection = 0;
                reference.TryGetValue(nodeNumber, out roadDirection);

                // This section checks which direction is the road facing and if there is a road on that node already
                int checkDirection = roadDirection & turn;
                if (checkDirection != 0)
                    nodeNumber++;
                else
                    break;

                // Return to start of the road to check if there is any more location for instantiation before the random placement location
                if (nodeNumber + 1 > numberOfNodes && numberOfIterations < 2)
                {
                    nodeNumber = 1;
                    numberOfIterations++;
                }

                // breaks the loop and exit the function if no more space for instantiation
                if (numberOfIterations > 1)
                    return;
            }

            // Exits if the node is outside the range of the parent road
            if (nodeNumber > numberOfNodes || numberOfIterations > 2)
            {
                nodeNumber = 15;
                return;
            }

            // Saves the position of the node and direction into the parent road's dictionary
            int addDirection = turn;
            if (reference.ContainsKey(nodeNumber))
            {
                reference[nodeNumber] = reference[nodeNumber] | addDirection;
            }
            else
                reference.Add(nodeNumber, addDirection);


            // Translate the position of the new road along the length of the previous road at set intervals
            m_marker.transform.position = startNode + (previousRoad.transform.forward * nodeNumber * roadSpacing * buildingLotSize);

            if (m_marker.transform.position.x < transform.position.x || m_marker.transform.position.x > transform.position.x + habitableMapArea.x ||
                m_marker.transform.position.z < transform.position.z || m_marker.transform.position.z > transform.position.z + habitableMapArea.y)
            {
                return;
            }

            // Create a reference to the start node of the new road
            Vector3 newStartNode = m_marker.transform.position;

            float intendedLength;
            // Creates a random length between 8 units and maximum road length units in length
            if (autoGenerate)
            {
                float variance = (maximumRoadUnitLength - minimumRoadUnitLength) * actualDensity + 1;
                intendedLength = UnityEngine.Random.Range(minimumRoadUnitLength, maximumRoadUnitLength - variance) * buildingLotSize;
            }

            else
                intendedLength = UnityEngine.Random.Range(minimumRoadUnitLength, maximumRoadUnitLength) * buildingLotSize;

            ////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // This is used to check for roads that are parallel to the current road which is road width and building spacing distance away from it
            RaycastHit[] hitColliders = Physics.SphereCastAll(m_marker.transform.position, roadWidth + buildingLotSize, m_marker.transform.forward, intendedLength, 1 << roadLayer/* | 1 << buildingLayer*/);

            float roadLength = intendedLength;
            //List<GameObject> buildingsOnRoad = new List<GameObject>();
            for (int i = 0; i < hitColliders.Length; i++ )
            {

                if (m_marker.transform != hitColliders[i].collider.transform)
                {
                    float dotProduct = Vector3.Dot(hitColliders[i].collider.transform.forward, m_marker.transform.forward);

                    // This is used to check if its detecting road on the opposite side of the previous road
                    bool oppositeRoad = false;

                    // This is to make sure that density is fully shown when maxed out
                    if (density > 0.9f)
                        oppositeRoad = (previousRoad == hitColliders[i].collider.GetComponent<Road>().parentRoad);
                    else
                        oppositeRoad = false;


                    if ((dotProduct > 0.99f || dotProduct < -0.99f) && !oppositeRoad)
                    {
                        if (roadLength > hitColliders[i].distance)
                            roadLength = hitColliders[i].distance;
                    }
                }
            }
            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            if (roadLength < 1)
            {
                return;
            }

            // Checks for any building on the road and destroys them if they are
            RaycastHit[] buildingCheck = Physics.SphereCastAll(newStartNode, roadWidth / 2, m_marker.transform.forward, roadLength, 1 << buildingLayer);
            for (int i = 0; i < buildingCheck.Length; i++)
            {
                if (buildingCheck[i].collider.gameObject.layer == buildingLayer)
                    Destroy(buildingCheck[i].collider.gameObject);
            }

            // Instantiate the road
            GameObject clone = Instantiate(roads, m_marker.transform.position, m_marker.transform.rotation);
            instantiatedRoad++;

            // Sets the layer, parent road reference and building layer reference
            clone.layer = 8;

            clone.AddComponent<Road>();

            clone.GetComponent<Road>().parentRoad = previousRoad;

            clone.GetComponent<Road>().buildingLayer = buildingLayer;


            // Scale the road in relation to the length specified
            clone.transform.localScale += new Vector3(0, 0, roadLength - 1);

            // Translate the road to half its length so that its end connects with the previous road
            clone.transform.Translate(new Vector3(0, 0, roadLength / 2));

            // Create a reference to the end of the road
            Vector3 newEndNode = clone.transform.position + clone.transform.forward * roadLength / 2;

            // This section is similar to main road where it instantiate new roads depending on the number of node the new parent road can accomodate
            int newNumberOfNodes = (int)((newEndNode - newStartNode).magnitude / (buildingLotSize * roadSpacing));
            int numBranches = (int)Math.Ceiling(newNumberOfNodes * 2 * actualDensity);
            numberOfRoads++;
            int numberOfSpace = newNumberOfNodes * roadSpacing;
            for (int i = 0; i < numBranches; i++)
            {
                nodeNumber = UnityEngine.Random.Range(1, newNumberOfNodes);
                if (numberOfRoads < maximumNoOfRoads && depth < maximumDepth)
                    CreateRoad(newStartNode, newEndNode, clone.transform.rotation, clone, nodeNumber, newNumberOfNodes, depth);
            }

            // This is used for setting up the building
            SetBuildingSpace(numberOfSpace, newStartNode, clone);
            return;
        }

        private void SetBuildingSpace(int numberOfSpace, Vector3 startNode, GameObject roadInstance)
        {
            // Sets the increment of checks in relation to the building lot size
            Vector3 forwardIncrement = roadInstance.transform.forward * buildingLotSize;
            Vector3 rightIncrement = roadInstance.transform.right * buildingLotSize;

            for (int spaceNumber = 0; spaceNumber < numberOfSpace; spaceNumber++)
            {
                // Sets the position to check of the building on the right side of the road
                Vector3 pos = startNode + forwardIncrement * spaceNumber + rightIncrement;

                // Check and instantiate building on the right
                InstantiateBuilding(startNode, pos, forwardIncrement, spaceNumber);

                // Checks the left side of the road
                pos -= rightIncrement * 2;

                // Check and instantiate building on the left
                InstantiateBuilding(startNode, pos, forwardIncrement, spaceNumber);
            }
        }

        private void InstantiateBuilding(Vector3 startNode, Vector3 pos, Vector3 forwardIncrement, int spaceNumber)
        {
            float detectionRadius = buildingLotSize / 2.01f;

            // Sets the layers to check
            LayerMask layersToCheck = 1 << roadLayer | 1 << buildingLayer;

            // Checks if there are any objects on that position
            Collider[] objectDetected = Physics.OverlapSphere(pos, detectionRadius, layersToCheck);

            if (objectDetected.Length != 0)
            {
                return;
            }

            int totalBuilding = (commercialBuildings.Count + residentialBuildings.Count);

            // Allocate areas for commercial and residential
            bool residentialArea = (int)(Mathf.PerlinNoise(pos.x / overallNoiseScale + overallOffset.x, pos.z / overallNoiseScale + overallOffset.y) * totalBuilding) < (7 * totalBuilding /10);
            List<Building> buildingSet = null;

            // Checks if the area is meant to be commercial or residential
            if (residentialArea)
                buildingSet = residentialBuildings;
            else
                buildingSet = commercialBuildings;

            // If there's no road or building in that particular space, instantiate a building in reference to Perlin noise in that position
            int buildingNumber = 0;
            if (totallyRandom)
                buildingNumber = UnityEngine.Random.Range(0, buildingSet.Count - 1) % (buildingSet.Count - 1);
            else
                buildingNumber = (int)(Mathf.PerlinNoise(pos.x / noiseScale + offset.x, pos.z / noiseScale + offset.y) * buildingSet.Count) % buildingSet.Count;
            //int buildingNumber = UnityEngine.Random.Range(0, buildingSet.Count - 1) % (buildingSet.Count - 1);

            bool useDefault = false;
            Vector2 allocation = buildingSet[buildingNumber].spaceAllocation;

            if (allocation.sqrMagnitude > 2)
            {
                // This is used to check every allocation space that put the shape of the building into consideration
                for (int i = 0; i < allocation.x; i++)
                {
                    for (int j = 0; j < allocation.y; j++)
                    {
                        Collider[] spaceCheck = Physics.OverlapSphere(pos + new Vector3(i, 0, j) * buildingLotSize, detectionRadius, layersToCheck);
                        if (spaceCheck.Length != 0)
                        {
                            useDefault = true;
                            break;
                        }
                    }
                }

            }

            // Creates reference to the building prefab and apply any correction needed for it
            GameObject buildingPrefab = buildingSet[buildingNumber].buildingObject;
            Vector3 directionToFace = new Vector3(0, 0, 0);

            if (useDefault)
            {
                if (residentialArea)
                    // Change the building to make to the default prefab when space is not big enough
                    buildingPrefab = defaultResidential.buildingObject;
                else
                    buildingPrefab = defaultCommercial.buildingObject;

                // Specify the direction of the road in reference to the default prefab allocation
                directionToFace = startNode + forwardIncrement * spaceNumber;
            }
            else
            {
                // Reposition to centre of the building instance
                pos = pos + new Vector3(allocation.x - 1, 0, allocation.y - 1) * buildingLotSize /2;

                // Specify the direction of the road
                directionToFace = startNode + forwardIncrement * spaceNumber + forwardIncrement * (allocation.x - 1) / 2;
            }


            GameObject buildingInstance = Instantiate(buildingPrefab, pos, Quaternion.identity);

            // Rotate the building so that it's facing the road
            buildingInstance.transform.LookAt(directionToFace);

            // Check if that particular prefab requires any model correction
            ModelCorrection correction = buildingInstance.GetComponent<ModelCorrection>();

            // Make the correction if required
            if (correction != null)
            {
                if (correction.correctRotation == false)
                    correction.FixRotation();
                if (correction.correctPosition == false)
                    correction.FixPosition();
                if (correction.correctScale == false)
                    correction.FixScale();
            }

            // Sets the building to the building layer
            buildingInstance.layer = buildingLayer;
        }


        // Update is called once per frame
        void Update()
        {
            // Switch between overhead camera and first person camera
            if (firstPersonCamera != null && overheadCamera != null && Input.GetKeyDown(KeyCode.M) && !steamVRConnected)
            {
                firstPersonCamera.SetActive(!firstPersonCamera.activeSelf);
                overheadCamera.SetActive(!overheadCamera.activeSelf);
            }
        }
    }

    // Road class where the road stores memory of its parent road as well as any road branching off from it 
    public class Road : MonoBehaviour
    {
        [HideInInspector] public Dictionary<int, int> occupiedNode = new Dictionary<int, int>();
        [HideInInspector] public GameObject parentRoad;
        [HideInInspector] public int buildingLayer;                         

        private void OnCollisionStay(Collision collision)
        {
            if (collision.gameObject.layer == buildingLayer)
                Destroy(collision.gameObject);
        }
    }
}