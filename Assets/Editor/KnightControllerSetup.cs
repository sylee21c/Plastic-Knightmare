using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class KnightControllerSetup
{
    private const string ControllerPath = "Assets/TinyKnights/Animations/KnightCont.controller";
    private const string AttackClipPath = "Assets/TinyKnights/Animations/Knights/KnightAttack.anim";
    private const string BlockClipPath  = "Assets/TinyKnights/Animations/Knights/KnightBlock.anim";

    [MenuItem("Tools/Setup Knight Controller (Attack/Block)")]
    public static void Setup()
    {
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null)
        {
            Debug.LogError($"컨트롤러를 찾을 수 없음: {ControllerPath}");
            return;
        }

        AnimationClip attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AttackClipPath);
        AnimationClip blockClip  = AssetDatabase.LoadAssetAtPath<AnimationClip>(BlockClipPath);
        if (attackClip == null || blockClip == null)
        {
            Debug.LogError("Attack 또는 Block 클립을 찾을 수 없음");
            return;
        }

        // ── 파라미터 추가 (중복 방지) ─────────────────────────────
        EnsureParameter(ctrl, "Speed",      AnimatorControllerParameterType.Float);
        EnsureParameter(ctrl, "Attack",     AnimatorControllerParameterType.Trigger);
        EnsureParameter(ctrl, "IsBlocking", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

        // ── 기존 상태 참조 ────────────────────────────────────────
        AnimatorState idleState    = FindState(sm, "KnightIdle");
        AnimatorState runningState = FindState(sm, "KnightRunning");
        if (idleState == null || runningState == null)
        {
            Debug.LogError("KnightIdle 또는 KnightRunning 상태를 찾을 수 없음");
            return;
        }

        // ── Attack / Block 상태 추가 (중복 시 재사용) ─────────────
        AnimatorState attackState = FindState(sm, "KnightAttack");
        if (attackState == null)
            attackState = sm.AddState("KnightAttack", new Vector3(500f, 0f, 0f));
        attackState.motion = attackClip;

        AnimatorState blockState = FindState(sm, "KnightBlock");
        if (blockState == null)
            blockState = sm.AddState("KnightBlock", new Vector3(500f, 120f, 0f));
        blockState.motion = blockClip;

        // ── 기존 전환 정리 (Attack/Block 관련만) ──────────────────
        ClearTransitionsToOrFrom(sm, attackState);
        ClearTransitionsToOrFrom(sm, blockState);

        // ── Idle/Running → Attack (Trigger) ───────────────────────
        AddTrigger(idleState, attackState, "Attack");
        AddTrigger(runningState, attackState, "Attack");

        // ── Attack → Idle (exit time) ─────────────────────────────
        AnimatorStateTransition attackExit = attackState.AddTransition(idleState);
        attackExit.hasExitTime = true;
        attackExit.exitTime = 0.9f;
        attackExit.duration = 0.15f;

        // ── Idle/Running → Block (IsBlocking = true) ──────────────
        AddBool(idleState, blockState, "IsBlocking", true);
        AddBool(runningState, blockState, "IsBlocking", true);

        // ── Block → Idle (IsBlocking = false) ─────────────────────
        AddBool(blockState, idleState, "IsBlocking", false);

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("KnightCont 컨트롤러에 Attack/Block 상태와 파라미터를 추가했습니다.");
    }

    private static void EnsureParameter(AnimatorController ctrl, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in ctrl.parameters)
            if (p.name == name) return;
        ctrl.AddParameter(name, type);
    }

    private static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        foreach (ChildAnimatorState c in sm.states)
            if (c.state.name == name) return c.state;
        return null;
    }

    private static void ClearTransitionsToOrFrom(AnimatorStateMachine sm, AnimatorState target)
    {
        // 이 상태에서 나가는 전환 전부 제거
        foreach (AnimatorStateTransition t in target.transitions)
            target.RemoveTransition(t);

        // 다른 상태에서 이 상태로 들어오는 전환 제거
        foreach (ChildAnimatorState c in sm.states)
        {
            var toRemove = new System.Collections.Generic.List<AnimatorStateTransition>();
            foreach (AnimatorStateTransition t in c.state.transitions)
                if (t.destinationState == target) toRemove.Add(t);
            foreach (AnimatorStateTransition t in toRemove)
                c.state.RemoveTransition(t);
        }
    }

    private static void AddTrigger(AnimatorState from, AnimatorState to, string trigger)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.1f;
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    private static void AddBool(AnimatorState from, AnimatorState to, string param, bool value)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.1f;
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
    }
}
