using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Firebase;
using Firebase.Extensions;
using Firebase.Firestore;
using System.Threading;
using System.Threading.Tasks;

public class CounterManager : MonoBehaviour
{
    public Text textTime;
    public Text textScoreHealth;
    public Text textScoreWeapons;
    public Text textAccountValue;

    private float time;  
    private float health;
    private float weapons;

    private string collectionPath = "scoring";
    protected string documentId = "";
    private DependencyStatus dependencyStatus = DependencyStatus.UnavailableOther;
    protected bool isFirebaseInitialized = false;


    // Start is called before the first frame update
    void Start()
    {
 
        time = 0;
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
            dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available) {
                InitializeFirebase();
            } else {
                 Debug.LogError(
                      "Could not resolve all Firebase dependencies: " + dependencyStatus);
            }
        });
    }

    protected virtual void InitializeFirebase() {
          isFirebaseInitialized = true;
          documentId = GameManager.instance.account;
          textAccountValue.text = documentId;
          Debug.LogFormat("account {0}",GameManager.instance.account);
          StartCoroutine(ReadDoc(GetDocumentReference()));
    }

    protected FirebaseFirestore db {
      get {
        return FirebaseFirestore.DefaultInstance;
      }
    }

    private CollectionReference GetCollectionReference() {
      return db.Collection(collectionPath);
    }

    private DocumentReference GetDocumentReference() {
      if (documentId == "") {
        return GetCollectionReference().Document();
      }
      return GetCollectionReference().Document(documentId);
    }
    // Update is called once per frame
    void Update()
    {

         time+=Time.deltaTime;
         textTime.text = time.ToString("F");
    }

    public void addHealth(float newhealth)
    {
        health += newhealth;
        textScoreHealth.text = health.ToString();

        var data = new Dictionary<string, object>{
            {"weapons", weapons},
            {"health", health},
          };
        StartCoroutine(WriteDoc(GetDocumentReference(), data));
    }

    public void addWeapons(float neweapons)
    {
        weapons += neweapons;
        textScoreWeapons.text = weapons.ToString();
        var data = new Dictionary<string, object>{
            {"weapons", weapons},
            {"health", health},
          };
        StartCoroutine(WriteDoc(GetDocumentReference(), data));

    }


    private IEnumerator WriteDoc(DocumentReference doc, IDictionary<string, object> data) {
      Task setTask = doc.SetAsync(data);
      yield return new WaitForTaskCompletion(setTask);
      if (!(setTask.IsFaulted || setTask.IsCanceled)) {
        // Update the collectionPath/documentId because:
        // 1) If the documentId field was empty, this will fill it in with the autoid. This allows
        //    you to manually test via a trivial 'click set', 'click get'.
        // 2) In the automated test, the caller might pass in an explicit docRef rather than pulling
        //    the value from the UI. This keeps the UI up-to-date. (Though unclear if that's useful
        //    for the automated tests.)
        collectionPath = doc.Parent.Id;
        documentId = doc.Id;

        Debug.Log("document added");
      } else {
        Debug.LogError("document not added");
      }
    }


    private IEnumerator ReadDoc(DocumentReference doc) {
      Task<DocumentSnapshot> getTask = doc.GetSnapshotAsync();
      yield return new WaitForTaskCompletion(getTask);
      if (!(getTask.IsFaulted || getTask.IsCanceled)) {
        DocumentSnapshot snap = getTask.Result;
        IDictionary<string, object> resultData = snap.ToDictionary();
        health = 0;
        weapons = 0;
        if (resultData.ContainsKey("health")) {
            textScoreHealth.text = resultData["health"].ToString();
            health = int.Parse(textScoreHealth.text);
        }
        if (resultData.ContainsKey("weapons")) {
            textScoreWeapons.text = resultData["weapons"].ToString();
            weapons = int.Parse(textScoreWeapons.text);
        }
        Debug.Log("document read");
      } else {
        Debug.LogError("document not read");
      }
    }

    class WaitForTaskCompletion : CustomYieldInstruction {
      Task task;
    
      // Create an enumerator that waits for the specified task to complete.
      public WaitForTaskCompletion(Task task) {
        this.task = task;
      }

      // Wait for the task to complete.
      public override bool keepWaiting {
        get {
          if (task.IsCompleted) {
            if (task.IsFaulted) {
              string s = task.Exception.ToString();
            }
            return false;
          }
          return true;
        }
      }
    }

}
