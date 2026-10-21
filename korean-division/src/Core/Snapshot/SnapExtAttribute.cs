namespace KoreanDivision.Core;

/// <summary>
/// v0.4 확장 구역(SUPL · CMB2 · CMDX)에서 저장하는 상태 필드 표시. 저장 완전성 감사(SnapshotSelfTest.Audit)가
/// 이 표시가 붙은 필드를 "저장됨"으로 인정한다 — 표시를 붙였으면 반드시 해당 partial 의 Write/Read 에서 저장할 것.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class SnapExtAttribute : Attribute { }
