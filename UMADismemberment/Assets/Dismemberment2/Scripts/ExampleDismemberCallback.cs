using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UMA.Dismemberment2;

public class ExampleDismemberCallback : MonoBehaviour
{
    public string boneName;
    public SkinnedMeshRenderer gibSplit;
    public Material gibSplitMaterial;
    public SkinnedMeshRenderer gibSource;
    public Material gibSourceMaterial;

    UmaDismemberment dismemberment;

    void Start()
    {
        dismemberment = gameObject.GetComponent<UmaDismemberment>();
        if (dismemberment != null)
            dismemberment.DismemberedEvent.AddListener(DismemberedCallback);
    }

    void DismemberedCallback(Transform root, Transform targetBone)
    {
        if (targetBone == null)
            return;

        if (targetBone.name != boneName)
            return;

        root.gameObject.AddComponent<Rigidbody>();
        SphereCollider c = targetBone.gameObject.AddComponent<SphereCollider>();
        c.center = new Vector3(-0.22f, 0f, 0.05f);
        c.radius = 0.12f;

        SkinnedMeshRenderer rootSmr = root.gameObject.GetComponent<SkinnedMeshRenderer>();

        SkinnedMeshRenderer gibSmr = CreateChildSmr(gibSplit, rootSmr);
        if (gibSmr != null)
        {
            gibSmr.material = gibSplitMaterial;

            SkinnedMeshRenderer sourceSmr = gameObject.transform.Find("UMARenderer").GetComponent<SkinnedMeshRenderer>();
            gibSmr = CreateChildSmr(gibSource, sourceSmr);
            gibSmr.material = gibSourceMaterial;
        }
    }

    static SkinnedMeshRenderer CreateChildSmr(SkinnedMeshRenderer smrPrefab, SkinnedMeshRenderer targetSmr)
    {
        if (smrPrefab == null || targetSmr == null)
            return null;

        SkinnedMeshRenderer newSmr = Instantiate<SkinnedMeshRenderer>(smrPrefab, targetSmr.transform);

        //Find common bones and copy them to the new renderer
        Transform[] rootBones = targetSmr.bones;
        Transform[] gibBones = newSmr.bones;
        for (int i = 0; i < gibBones.Length; i++)
        {
            for (int j = 0; j < rootBones.Length; j++)
            {
                if (gibBones[i].name == rootBones[j].name)
                {
                    gibBones[i] = rootBones[j];

                    if (gibBones[i].name == newSmr.rootBone.name)
                        newSmr.rootBone = rootBones[j];

                    break;
                }
            }
        }
        newSmr.bones = gibBones;
        return newSmr;
    }
}
