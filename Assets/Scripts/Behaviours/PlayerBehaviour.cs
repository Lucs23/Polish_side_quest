using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using ObjectPool;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

public class PlayerBehaviour : MonoBehaviour
{
    new private Rigidbody2D rigidbody;
    private Camera mainCamera;

    [SerializeField]
    private float movementSpeed = 5f;
    private float stamina = 100f;
    private Slider staminaBar;
    private AudioSource creakAudioSource;
    private AudioSource heartbeatSource;
    private AudioClip[] creakClips;
    private float stepTimer = 0f;
    private Light2D playerLight;
    private float heartbeatTimer;

    public float MovementSpeed { get { return movementSpeed; } set { movementSpeed = value; } }

    private float targetAngle;  // The angle we want to reach
    [SerializeField]
    private float rotationSpeed = 90f, rotationTime = 0.15f;  // The speed at which we rotate (degrees per second)
    private float currentAngle;  // The current angle of the playerTransform
    private float rotationProgress = 0;  // Progress of the current rotation

    Transform pivot;

    private void Start()
    {
        playerLight = gameObject.AddComponent<Light2D>();
        creakAudioSource = gameObject.AddComponent<AudioSource>();
        heartbeatSource = gameObject.AddComponent<AudioSource>();
        staminaBar = GameObject.Find("Stamina_bar").GetComponent<Slider>();

        mainCamera = Camera.main;
        rigidbody = GetComponent<Rigidbody2D>();
        pivot = transform.Find("PivotPoint");
        mainCamera.transform.position = new Vector3(transform.position.x, transform.position.y, -19.8f);

        creakAudioSource.playOnAwake = false;
        creakAudioSource.loop = false;
        creakClips = Resources.LoadAll<AudioClip>("Creaks");

        heartbeatSource.clip = Resources.Load<AudioClip>("heartbeat_single");
        heartbeatSource.loop = true;
        heartbeatSource.volume = 0.3f;
        heartbeatSource.Play();

        playerLight.pointLightInnerRadius = 1.5f;
        playerLight.pointLightOuterRadius = 5f;
        playerLight.intensity = 1.2f;
        playerLight.color = new Color(1f, 0.95f, 0.8f);
    }

    private void FixedUpdate()
    {
        Vector2 moveDirection = Vector2.zero;

        if (Input.GetKey(KeyCode.Space))
        {
            if (stamina > 0)
            {
                movementSpeed = 9f;
                stamina -= 5f;
            }
            else
            {
                movementSpeed = 5f;
            }
            staminaBar.value = stamina;
        }
        else
        {
            if (stamina < 100)
            {
                stamina += 0.5f;
            }
            staminaBar.value = stamina;
            movementSpeed = 5f;
        }
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            moveDirection.x = 1;
        }
        else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            moveDirection.x = -1;
        }
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            moveDirection.y = 1;
        }
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            moveDirection.y = -1;
        }

        float speed = moveDirection.magnitude;

        // moveDirection.Normalize();
        Vector2 targetVelocity = moveDirection * movementSpeed;
        rigidbody.linearVelocity = Vector2.Lerp(rigidbody.linearVelocity, targetVelocity, (Time.deltaTime / 1.5f) * MovementSpeed);

        if (speed > 0)
        {
            stepTimer -= Time.deltaTime;
            RotatePlayer(moveDirection);
        }
        else
        {
            stepTimer = 0.05f;
        }
        if (stepTimer < 0f && creakClips.Length > 0)
        {
            int randomIndex = Random.Range(0, creakClips.Length);
            creakAudioSource.pitch = Random.Range(0.85f, 1.15f);
            creakAudioSource.PlayOneShot(creakClips[randomIndex]);
            if (Input.GetKey(KeyCode.Space))
            {
                stepTimer = 0.6f;
            }
            else
            {
                stepTimer = 1f;
            }
        }
        int enemyLayer = 1 << (int)CollisionLayer.Enemy;
        Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(transform.position, 5f, enemyLayer);
        int enemyCount = nearbyEnemies.Length;

        float dangerFactor = Mathf.InverseLerp(2f, 6f, enemyCount);

        float targetPitch = Mathf.Lerp(0.9f, 1.8f, dangerFactor);
        float targetVolume = Mathf.Lerp(0.25f, 0.85f, dangerFactor);
        float targetTimeScale = enemyCount >= 3 ? Mathf.Lerp(0.75f, 0.45f, Mathf.InverseLerp(3f, 6f, enemyCount)) : 1f;

        heartbeatSource.pitch = Mathf.Lerp(heartbeatSource.pitch, targetPitch, Time.unscaledDeltaTime * 3f);
        heartbeatSource.volume = Mathf.Lerp(heartbeatSource.volume, targetVolume, Time.unscaledDeltaTime * 3f);
        Time.timeScale = Mathf.Lerp(Time.timeScale, targetTimeScale, Time.unscaledDeltaTime * 4f);

        ConfinePlayerToWorldBoundaries();
    }

    public void ConfinePlayerToWorldBoundaries()
    {
        // Predict the next position based on the current velocity
        Vector2 nextPosition = rigidbody.position + rigidbody.linearVelocity * Time.fixedDeltaTime;

        // Create a copy of the velocity vector
        Vector2 newVelocity = rigidbody.linearVelocity;

        // Check if the next position is outside the world bounds and correct the velocity
        if (nextPosition.x < GenerateWorldBehaviour.PlayArea.xMin)
        {
            newVelocity.x = 0;
        }
        else if (nextPosition.x > GenerateWorldBehaviour.PlayArea.xMax)
        {
            newVelocity.x = 0;
        }

        if (nextPosition.y < GenerateWorldBehaviour.PlayArea.yMin)
        {
            newVelocity.y = 0;
        }
        else if (nextPosition.y > GenerateWorldBehaviour.PlayArea.yMax)
        {
            newVelocity.y = 0;
        }

        // Update the velocity
        rigidbody.linearVelocity = newVelocity;
    }

    private void RotatePlayer(Vector2 moveDirection)
    {
        // Calculate the new target angle
        float newTargetAngle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg - 90;

        // If the target angle has changed, start a new rotation
        if (Mathf.Abs(newTargetAngle - targetAngle) > 0.01f)
        {
            targetAngle = newTargetAngle;
            currentAngle = transform.rotation.eulerAngles.z;

            // Calculate the shortest direction for the rotation
            float deltaAngle = Mathf.DeltaAngle(currentAngle, targetAngle);
            targetAngle = currentAngle + deltaAngle;

            rotationProgress = 0;
        }

        // Perform the rotation over several frames
        if (rotationProgress < 1)
        {
            rotationProgress += Time.fixedDeltaTime / rotationTime;
            float angle = Mathf.LerpAngle(currentAngle, targetAngle, rotationProgress);
            transform.RotateAround(pivot.position, Vector3.forward, angle - transform.eulerAngles.z);
        }
    }
}
