using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class Kucing : MonoBehaviour
{
    public NavMeshAgent kucing;
    public GameObject player;
    public Animator kucingAnimator; 
    public AudioSource kucingAudio; 
    public AudioClip meowClip; 
    public AudioClip walkClip; 
    public float roamRadius = 20f;
    public float timeToApproachPlayer = 5f;

    private float distanceToPlayer;
    private bool isJumping = false;
    private bool isApproachingPlayer = false;
    private float approachTimer = 0f;

    void Start()
    {
        kucing = GetComponent<NavMeshAgent>();
        distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
    }

    void Update()
    {
        DetectPlayer();
        HandleMovement();
    }

    private void DetectPlayer()
    {
        distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

    
        if (distanceToPlayer <= 10f && !isApproachingPlayer)
        {
            isApproachingPlayer = true;
            approachTimer = timeToApproachPlayer;
            kucingAudio.PlayOneShot(meowClip);
        }
    }

    private void HandleMovement()
    {
        if (isApproachingPlayer)
        {
            approachTimer -= Time.deltaTime;

        
            kucing.destination = player.transform.position;
            kucingAnimator.SetBool("isWalking", true);
            kucingAudio.clip = walkClip;
            if (!kucingAudio.isPlaying)
                kucingAudio.Play();

            if (approachTimer <= 0f)
            {
                isApproachingPlayer = false; 
                RoamAround(); 
            }
        }
        else
        {
            RoamAround();
        }
    }

    private void RoamAround()
    {
        // If the cat isn't already jumping, make it roam
        if (!isJumping)
        {
            // Random position to roam
            Vector3 randomDirection = Random.insideUnitSphere * roamRadius;
            randomDirection += transform.position;
            NavMeshHit hit;
            NavMesh.SamplePosition(randomDirection, out hit, roamRadius, 1);
            Vector3 finalPosition = hit.position;

            //kucing.destination = finalPosition;

            kucingAnimator.SetBool("isWalking", true); // Walking animation
            kucingAudio.clip = walkClip;
            if (!kucingAudio.isPlaying)
                kucingAudio.Play();

            // Randomly trigger jump
            if (Random.Range(0, 100) < 10) 
            {
                StartCoroutine(Jump());
            }
        }
    }

    private IEnumerator Jump()
    {
        isJumping = true;
        kucingAnimator.SetTrigger("Jump"); 
        yield return new WaitForSeconds(2f); 
        isJumping = false;

    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, player.transform.position);
        Gizmos.DrawWireSphere(transform.position, roamRadius);
    }
}
