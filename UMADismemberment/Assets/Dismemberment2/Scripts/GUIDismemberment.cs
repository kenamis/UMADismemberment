using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UMA.CharacterSystem;
using UMA.Dismemberment2;

public class GUIDismemberment : MonoBehaviour
{
    public GameObject avatar;
    public HumanBodyBones boneToSlice;

    UmaDismemberment dismember;

    Button button;

    void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);

        if(avatar != null)
        {
            dismember = avatar.GetComponent<UmaDismemberment>();
        }
    }

    void OnClick()
    {
        if (dismember == null)
            Debug.LogError("UmaDismemberment not found!");

        UmaDismemberment.DismemberedInfo info;
        dismember.Slice(boneToSlice, out info);

        //example hook up copied from callback
        if (info.targetBone == null)
            return;

        /*info.root.gameObject.AddComponent<Rigidbody>();

        GameObject go = info.targetBone.gameObject;
        CapsuleCollider c = go.AddComponent<CapsuleCollider>();
        c.direction = 0;
        c.radius = 0.1f;
        c.height = 0.6f;
        c.center = new Vector3(-0.3f, 0f, 0f);*/
    }
}
