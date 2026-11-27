
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using System.Linq;

public static class SitPreviewBuild
{
    const string Output = "Assets/KeepDriving/";
    const string PreviewScene = "Assets/Scenes/BubbaSitPreview.unity";
    static Transform Bone(Animator rig, HumanBodyBones bone) =>
        rig.GetBoneTransform(bone) ?? throw new System.InvalidOperationException("Missing bone " + bone);

    [MenuItem("Keep Driving/Create Bubba Sit Preview")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewScene))
        {
            EditorSceneManager.OpenScene(PreviewScene);
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Bubba_The_Handyman/URP/Prefabs/Handyman_ver_5.prefab");
        var bubba = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        bubba.name = "Bubba";
        bubba.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var rig = bubba.GetComponent<Animator>();
        rig.applyRootMotion = false;
        var idleAsset = AssetDatabase.GUIDToAssetPath("d3e27e3d0c7e7864297295a0b4e5b375");
        var idle = AssetDatabase.LoadAllAssetsAtPath(idleAsset).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        var graph = PlayableGraph.Create();
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var output = AnimationPlayableOutput.Create(graph, "Idle pose", rig);
        output.SetSourcePlayable(AnimationClipPlayable.Create(graph, idle));
        graph.Play();
        graph.Evaluate(0f);

        var bones = bubba.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .SelectMany(renderer => renderer.bones).Where(bone => bone && bone != bubba.transform).Distinct().ToArray();
        var initial = bones.Select(bone => bone.localRotation).ToArray();
        var hips = Bone(rig, HumanBodyBones.Hips);
        if (!bones.Contains(hips)) throw new System.Exception("Hips absent from animated hierarchy");
        var initialHips = hips.localPosition;
        var initialHipWorld = hips.position;
        var leftFoot = Bone(rig, HumanBodyBones.LeftFoot);
        var rightFoot = Bone(rig, HumanBodyBones.RightFoot);
        var leftFootWorld = leftFoot.position;
        var rightFootWorld = rightFoot.position;
        var leftFootRotation = leftFoot.rotation;
        var rightFootRotation = rightFoot.rotation;
        var leftThigh = Bone(rig, HumanBodyBones.LeftUpperLeg);
        var leftKnee = Bone(rig, HumanBodyBones.LeftLowerLeg);
        var legLength = Vector3.Distance(leftThigh.position, leftKnee.position) + Vector3.Distance(leftKnee.position, leftFoot.position);
        graph.Destroy();

        var clip = new AnimationClip { name = "Bubba Stand To Sit", frameRate = 30f };
        var curves = bones.Select(_ => new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() }).ToArray();
        var position = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
        for (int frame = 0; frame <= 57; frame += 3)
        {
            float t = frame / 30f;
            float sit = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.10f, 1.15f, t));
            float reach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.08f, .86f, t));
            for (int i = 0; i < bones.Length; i++) bones[i].localRotation = initial[i];
            hips.localPosition = initialHips;
            hips.position = initialHipWorld + legLength * sit * new Vector3(0f, -.46f, -.17f);
            float lean = Mathf.Sin(sit * Mathf.PI) * 17f + sit * 4f;
            Bone(rig, HumanBodyBones.Spine).rotation = Quaternion.AngleAxis(lean, Vector3.right) * Bone(rig, HumanBodyBones.Spine).rotation;
            Bone(rig, HumanBodyBones.Head).rotation = Quaternion.AngleAxis(-lean * .8f, Vector3.right) * Bone(rig, HumanBodyBones.Head).rotation;
            PoseLeg(rig, true, leftFootWorld, leftFootRotation, sit, legLength);
            PoseLeg(rig, false, rightFootWorld, rightFootRotation, sit, legLength);
            PoseArm(rig, true, reach);
            PoseArm(rig, false, reach);
            for (int i = 0; i < bones.Length; i++)
            {
                var q = bones[i].localRotation;
                curves[i][0].AddKey(t, q.x); curves[i][1].AddKey(t, q.y);
                curves[i][2].AddKey(t, q.z); curves[i][3].AddKey(t, q.w);
            }
            for (int axis = 0; axis < 3; axis++) position[axis].AddKey(t, hips.localPosition[axis]);
        }
        for (int i = 0; i < bones.Length; i++)
        {
            string path = AnimationUtility.CalculateTransformPath(bones[i], bubba.transform);
            for (int axis = 0; axis < 4; axis++)
                clip.SetCurve(path, typeof(Transform), "localRotation." + "xyzw"[axis], curves[i][axis]);
        }
        string hipPath = AnimationUtility.CalculateTransformPath(hips, bubba.transform);
        for (int axis = 0; axis < 3; axis++)
            clip.SetCurve(hipPath, typeof(Transform), "localPosition." + "xyz"[axis], position[axis]);
        clip.EnsureQuaternionContinuity();
        if (clip.length < 1.89f || AnimationUtility.GetCurveBindings(clip).Length < 30)
            throw new System.Exception("Incomplete clip");
        AssetDatabase.CreateAsset(clip, Output + "BubbaStandToSit.anim");

        for (int i = 0; i < bones.Length; i++) bones[i].localRotation = initial[i];
        hips.localPosition = initialHips;
        var controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(Output + "BubbaSitPreview.controller", clip);
        // The baked clip targets bone transforms directly. The humanoid avatar would claim those bindings.
        rig.avatar = null;
        rig.runtimeAnimatorController = controller;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Preview Floor";
        floor.transform.localScale = Vector3.one * .7f;
        floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Output + "Ground.mat");
        var camera = new GameObject("Preview Camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(3.0f, 1.75f, 3.35f);
        camera.transform.LookAt(new Vector3(0f, .94f, .1f));
        camera.fieldOfView = 34f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.14f, .19f, .25f);
        camera.tag = "MainCamera";
        var light = new GameObject("Key Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.6f;
        light.transform.rotation = Quaternion.Euler(42f, -30f, 0f);
        var fill = new GameObject("Fill Light").AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = .6f;
        fill.transform.rotation = Quaternion.Euler(25f, 110f, 0f);
        EditorSceneManager.SaveScene(scene, PreviewScene);
        AssetDatabase.SaveAssets();
        Debug.Log("BUBBA_SIT_PREVIEW_READY " + legLength + "m leg, " + clip.length + "s clip");
    }

    static void Aim(Transform joint, Transform child, Vector3 direction, float weight)
    {
        var delta = Quaternion.FromToRotation(child.position - joint.position, direction);
        joint.rotation = Quaternion.Slerp(Quaternion.identity, delta, weight) * joint.rotation;
    }

    static void PoseLeg(Animator rig, bool left, Vector3 originalFoot, Quaternion rotation, float sit, float length)
    {
        var thigh = Bone(rig, left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
        var knee = Bone(rig, left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
        var foot = Bone(rig, left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
        float weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .22f, sit));
        Vector3 target = originalFoot + length * sit * new Vector3(left ? -.025f : .025f, 0f, .38f);
        Vector3 offset = target - thigh.position;
        Vector3 axis = offset.normalized;
        float upper = Vector3.Distance(thigh.position, knee.position);
        float lower = Vector3.Distance(knee.position, foot.position);
        float distance = Mathf.Clamp(offset.magnitude, Mathf.Abs(upper - lower) + .0001f, upper + lower - .0001f);
        float along = (upper * upper - lower * lower + distance * distance) / (2f * distance);
        float bend = Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
        Vector3 kneeOffset = axis * along + Vector3.ProjectOnPlane(Vector3.forward, axis).normalized * bend;
        Aim(thigh, knee, kneeOffset, weight);
        Aim(knee, foot, target - knee.position, weight);
        foot.rotation = Quaternion.Slerp(foot.rotation, rotation, weight);
    }

    static void PoseArm(Animator rig, bool left, float reach)
    {
        var upper = Bone(rig, left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
        var forearm = Bone(rig, left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
        var hand = Bone(rig, left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        Aim(upper, forearm, new Vector3(left ? -.17f : .17f, -.60f, .76f), reach);
        Aim(forearm, hand, new Vector3(left ? .22f : -.22f, .30f, .91f), reach);
    }
}

