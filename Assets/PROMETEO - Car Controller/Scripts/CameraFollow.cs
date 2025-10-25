using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{

    public Transform carTransform;
    [Range(1, 10)]
    public float followSpeed = 2;
    [Range(1, 10)]
    public float lookSpeed = 5;
    Vector3 initialCameraPosition;
    Vector3 initialCarPosition;
    Vector3 absoluteInitCameraPosition;

    void Start()
    {
        if (carTransform == null)
        {
            Debug.LogError("CameraFollow: Car Transform is not assigned!", this);
            this.enabled = false; // Disable script if target is missing
            return;
        }
        initialCameraPosition = gameObject.transform.position;
        initialCarPosition = carTransform.position;
        absoluteInitCameraPosition = initialCameraPosition - initialCarPosition;
    }

    void FixedUpdate()
    {
        if (carTransform == null) return; // Stop if carTransform is missing

        //Look at car
        Vector3 _lookDirection = (new Vector3(carTransform.position.x, carTransform.position.y, carTransform.position.z)) - transform.position;

        // --- Added Check ---
        // Prevent LookRotation error if direction is zero
        if (_lookDirection != Vector3.zero)
        {
            Quaternion _rot = Quaternion.LookRotation(_lookDirection, Vector3.up);
            transform.rotation = Quaternion.Lerp(transform.rotation, _rot, lookSpeed * Time.deltaTime);
        }
        // --- End Check ---


        //Move to car
        Vector3 _targetPos = absoluteInitCameraPosition + carTransform.transform.position;
        transform.position = Vector3.Lerp(transform.position, _targetPos, followSpeed * Time.deltaTime);
    }
}