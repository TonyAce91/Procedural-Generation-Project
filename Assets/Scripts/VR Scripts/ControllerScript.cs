using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This script is adapted from the Steam VR teleporter script but specifically used for teleporting to any type of collider
[RequireComponent(typeof(SteamVR_TrackedObject))]
[RequireComponent(typeof(SteamVR_TrackedController))]
[RequireComponent(typeof(LineRenderer))]
public class ControllerScript : MonoBehaviour
{

    [Header("Teleport Settings")]
    public SteamVR_Controller.Device controllerDevice;
    public Valve.VR.EVRButtonId teleportButton = Valve.VR.EVRButtonId.k_EButton_SteamVR_Touchpad;
    [SerializeField] private float maxDistance = 10;
    private bool wasClicked = false;
    private LineRenderer teleporterLine;

    // This is used to find camera rig reference for steam VR
    Transform Reference
    {
        get
        {
            var top = SteamVR_Render.Top();
            return (top != null) ? top.origin : null;
        }
    }



    private void Start()
    {
        controllerDevice = SteamVR_Controller.Input((int)GetComponent<SteamVR_TrackedObject>().index);
        teleporterLine = GetComponent<LineRenderer>();
        teleporterLine.enabled = false;
    }

    private void Update()
    {
        if (controllerDevice.GetPressDown(teleportButton))
        {
            // This creates a line using line renderer to show where the player is aiming their controller
            teleporterLine.enabled = true;
            teleporterLine.SetPosition(1, new Vector3(0, 0, maxDistance));
            teleporterLine.endWidth = maxDistance / 1000;
        }
        if (controllerDevice.GetPressUp(teleportButton))
        {
            // Reference of the camera rig
            var cameraRig = Reference;
            if (cameraRig == null)
                return;

            // Create a plane at the Y position of the Play Area
            Plane plane = new Plane(Vector3.up, -cameraRig.position.y);

            // Initialise variables used for raycasting
            bool teleportableLocation = false;
            float dist = 0f;
            RaycastHit hitInfo;

            // Creates a raycast from the controller outwards to check for any teleportable locations
            teleportableLocation = Physics.Raycast(transform.position, transform.forward, out hitInfo, maxDistance);
            dist = hitInfo.distance;

            if (teleportableLocation)
            {
                // Get the current Camera (head) position on the ground relative to the world
                Vector3 headGroundPos = SteamVR_Render.Top().head.position;
                headGroundPos.y = cameraRig.position.y;

                // We need to translate the reference space along the vector between the head's position on the ground and the intersection point on the ground
                cameraRig.position = cameraRig.position + (transform.position + (transform.forward * dist)) - headGroundPos;
            }
            teleporterLine.enabled = false;
        }
    }
}
