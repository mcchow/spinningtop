using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class spinningTop : MonoBehaviour
{
    [Header("Bowl Settings")]
    public Vector3 bowlCenter = Vector3.zero;
    public Vector3 bowlRadii = new Vector3(2000f, 2000f, 1000f);
    public float slopeAccel = 800f;
    
    [Header("Spin Settings")]
    public float spinImpulse = 10000f;
    public Vector3 spinAxis = Vector3.forward;
    public float maxSpinSpeed = 3000f;
    public float minSpinSpeed = 10f; // Below this, beyblade is "knocked out"
    public float naturalSpinDecay = 0.5f; // Spin loss per second
    
    [Header("Collision Settings")]
    public float baseSpinLossOnHit = 20f; // Base spin loss when hit
    public float spinLossRandomRange = 10f; // Random variance
    public float spinAdvantageMultiplier = 0.5f; // Higher spin = less loss
    public ParticleSystem collisionParticles; // Assign in inspector

    [Header("Sound Settings")]
    public AudioSource audioHitSource;

    private Rigidbody rb;
    private float currentSpinSpeed;
    private bool isKnockedOut = false;
    
    // Public property to check spin health
    public float SpinHealthPercent => Mathf.Clamp01(currentSpinSpeed / maxSpinSpeed);
    public bool IsKnockedOut => isKnockedOut;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.mass = 60f;
        rb.drag = 0.5f;
        rb.angularDrag = 0.02f;
        rb.maxAngularVelocity = 3000f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        
        // Physics material
        PhysicMaterial lowFriction = new PhysicMaterial();
        lowFriction.dynamicFriction = 0.01f;
        lowFriction.staticFriction = 0.01f;
        lowFriction.bounciness = 0.0f;
        lowFriction.frictionCombine = PhysicMaterialCombine.Minimum;
        GetComponent<Collider>().material = lowFriction;
        
        // Lower center of mass
        rb.centerOfMass = new Vector3(0, -0.03f, 0);

        // Random initial position within bowl
        Vector3 randomPoint = Random.insideUnitSphere * 60f;
        // Initial conditions
        if (randomPoint.y > 0)
        {
            randomPoint.y = -randomPoint.y;
        }

        rb.AddForce(randomPoint, ForceMode.VelocityChange);
        rb.AddTorque(transform.TransformDirection(spinAxis).normalized * spinImpulse, ForceMode.Impulse);
        
        // Initialize spin speed
        currentSpinSpeed = spinImpulse;

        audioHitSource.enabled = true;
    }
    
    void FixedUpdate()
    {
        if (isKnockedOut) return;
        
        // Natural spin decay over time
        currentSpinSpeed -= naturalSpinDecay * Time.fixedDeltaTime;
        
        // Check if knocked out
        if (currentSpinSpeed <= minSpinSpeed)
        {
            KnockOut();
            return;
        }
        
        // Apply current spin speed to the rigidbody
        Vector3 currentAngularVel = rb.angularVelocity;
        Vector3 spinAxisWorld = transform.TransformDirection(spinAxis);
        float actualSpin = Vector3.Dot(currentAngularVel, spinAxisWorld);
        
        // Adjust angular velocity to match current spin speed
        if (Mathf.Abs(actualSpin) < currentSpinSpeed * 0.9f)
        {
            rb.AddTorque(spinAxisWorld * (currentSpinSpeed - actualSpin) * 0.1f, ForceMode.VelocityChange);
        }
        
        // Compute ellipsoid normal at current position
        Vector3 p = transform.position - bowlCenter;
        Vector3 n = new Vector3(
            p.x / (bowlRadii.x * bowlRadii.x),
            p.y / (bowlRadii.y * bowlRadii.y),
            p.z / (bowlRadii.z * bowlRadii.z)
        ).normalized;
        
        // Gyroscopic stabilization
        Vector3 angularVel = rb.angularVelocity;
        float spinSpeed = Vector3.Dot(angularVel, spinAxisWorld);
        
        if (Mathf.Abs(spinSpeed) > 1f)
        {
            Vector3 tiltAxis = Vector3.Cross(spinAxisWorld, n);
            float tiltAngle = Vector3.Angle(spinAxisWorld, n);
            Vector3 precessionTorque = tiltAxis.normalized * spinSpeed * tiltAngle * 0.01f;
            rb.AddTorque(precessionTorque, ForceMode.Force);
        }
        
        // Tangential component of gravity
        Vector3 g = Physics.gravity;
        Vector3 tangent = Vector3.ProjectOnPlane(g, n);
        rb.AddForce(tangent.normalized * slopeAccel, ForceMode.Acceleration);
        
        // Enhanced jitter
        if (rb.velocity.sqrMagnitude < 0.01f && Mathf.Abs(spinSpeed) > 10f)
        {
            rb.AddForce(Random.insideUnitSphere * 5f, ForceMode.Acceleration);
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        // Check if colliding with another spinning top
        spinningTop otherTop = collision.gameObject.GetComponent<spinningTop>();
        
        if (otherTop != null && !otherTop.IsKnockedOut && !isKnockedOut)
        {
            HandleBeybladeCollision(otherTop, collision);
        }
    }
    
    void HandleBeybladeCollision(spinningTop otherTop, Collision collision)
    {
        // Calculate spin advantage (higher spin = less damage)
        float spinRatio = currentSpinSpeed / (currentSpinSpeed + otherTop.currentSpinSpeed);
        float spinAdvantage = Mathf.Lerp(1f, spinAdvantageMultiplier, spinRatio);
        
        // Calculate spin loss with randomness
        float randomFactor = Random.Range(-spinLossRandomRange, spinLossRandomRange);
        float spinLoss = (baseSpinLossOnHit + randomFactor) * spinAdvantage;
        
        // Apply spin loss
        currentSpinSpeed = Mathf.Max(0, currentSpinSpeed - spinLoss);
        
        // Spawn particles at collision point
        if (collisionParticles != null && collision.contacts.Length > 0)
        {
            Vector3 collisionPoint = collision.contacts[0].point;
            ParticleSystem particles = Instantiate(collisionParticles, collisionPoint, Quaternion.identity);
            
            // Scale particle intensity based on collision force
            var main = particles.main;
            main.startSpeed = collision.relativeVelocity.magnitude * 0.1f;
            
            particles.Play();
            audioHitSource.Play();
            Destroy(particles.gameObject, 2f);
        }
        
        // Add impact force
        Vector3 impactDir = (transform.position - otherTop.transform.position).normalized;
        rb.AddForce(impactDir * collision.relativeVelocity.magnitude * 0.5f, ForceMode.Impulse);
        
        Debug.Log($"{gameObject.name} lost {spinLoss:F1} spin. Current: {currentSpinSpeed:F1}");
    }
    
    void KnockOut()
    {
        isKnockedOut = true;
        Debug.Log($"{gameObject.name} is knocked out!");
        
        // Optionally stop physics or change behavior
        rb.angularDrag = 2f; // Increase drag to slow down faster
    }
    
    // Public method to add spin (for power-ups, etc.)
    public void AddSpin(float amount)
    {
        currentSpinSpeed = Mathf.Min(maxSpinSpeed, currentSpinSpeed + amount);
    }
    
    // Debug visualization
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        
        // Draw spin health bar above beyblade
        Vector3 barPosition = transform.position + Vector3.up * 2f;
        float barWidth = 1f;
        float barHeight = 0.1f;
        
        // Background (red)
        Gizmos.color = Color.red;
        Gizmos.DrawCube(barPosition, new Vector3(barWidth, barHeight, 0.01f));
        
        // Foreground (green, based on spin health)
        Gizmos.color = Color.Lerp(Color.red, Color.green, SpinHealthPercent);
        Vector3 healthBarPos = barPosition - Vector3.right * barWidth * 0.5f * (1f - SpinHealthPercent);
        Gizmos.DrawCube(healthBarPos, new Vector3(barWidth * SpinHealthPercent, barHeight, 0.02f));
    }
}