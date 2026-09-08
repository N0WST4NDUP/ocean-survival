# CLAUDE.md — ShipSurivor 개발 가이드

> 본 문서는 1인 개발 미니게임 프로젝트의 **개발 방향과 프로세스**를 정의한다.
> 게임 디자인/콘텐츠 사양은 [Docs/GDD.md](Docs/GDD.md)에, 작업 계획/회고는 별도 문서로 분리한다.
> CLAUDE.md는 **얇게** 유지하고, 상세 내용은 하위 문서로 링크한다.

---

## 1. 프로젝트 개요

- **제목(가제)**: ShipSurivor
- **장르**: Roguelite Bullet Heaven
- **엔진**: Unity 6000.3.15f1 (URP)
- **기간**: 2026.05.18 ~ 2026.06.05 (1인 개발)
- **플랫폼**: PC(Windows) 우선

---

## 2. 문서 인덱스

| 문서 | 용도 |
| --- | --- |
| [Docs/GDD.md](Docs/GDD.md) | 게임 디자인 문서. 코어 루프 / 메커닉 / 시스템 / 마일스톤 / 리스크. **모든 디자인 의사결정의 근거.** |
| [Docs/Balancing.md](Docs/Balancing.md) | 밸런싱 수치 기록. 영역 / 값 / 근거 3요소. GDD의 `❓` 값들이 확정되면 여기로 이동. |
| [Docs/Boss/PirateLord.md](Docs/Boss/PirateLord.md) | Pirate Lord(해적왕) 보스 명세 — 페이즈/패턴/3페이즈 유령선 메커니즘. GDD §5.6 구체화. |
| [Docs/superpowers/specs/2026-09-08-readme-webgl-deploy-design.md](Docs/superpowers/specs/2026-09-08-readme-webgl-deploy-design.md) | README 작성 + WebGL 빌드의 GitHub Pages 배포/임베드 설계. Firebase의 WebGL 미지원 대응 포함. |

> 새 문서를 추가하면 **이 표에 한 줄로** 등록한다. 표 외 위치에 흩어진 문서는 없도록 관리.

### 향후 추가될 문서 (placeholder)

- `Docs/Architecture.md` — 코드 구조, 폴더 규약, 핵심 매니저/시스템 다이어그램.
- `Docs/Postmortem.md` — W3 종료 후 회고.

---

## 3. 개발 원칙

- **GDD가 진실의 원천(SSoT)**. 코드와 GDD가 충돌하면 GDD 우선 — 코드를 맞추거나 GDD를 의식적으로 갱신.
- **스코프 방어**: GDD §9 *OUT OF SCOPE* 항목은 코드/이슈/PR에서 발견 시 즉시 반려.
- **수치는 코드에 박지 않는다**: 밸런싱 수치는 ScriptableObject 또는 별도 데이터로 분리해 플레이테스트 중 핫스왑 가능하게.
- **객체 풀링 강제**: 적/투사체/드롭/VFX 등 다량 생성 객체는 반드시 풀링 (§5.3.2 부하 테스트 임계 준수).
- **일반 몹 투사체 금지** (GDD §5.3) — 접촉/즉발 데미지만. 위반 PR은 머지 금지.

---

## 4. Git 워크플로우

### 브랜치 전략

- `main` — 보호 브랜치. 직접 푸시 금지.
- 작업 브랜치 prefix:
  - `feature/<이슈번호>-<요약>` — 새 기능
  - `fix/<이슈번호>-<요약>` — 버그 수정
  - `chore/<이슈번호>-<요약>` — 빌드/설정/도구
  - `docs/<이슈번호>-<요약>` — 문서만 변경
  - `refactor/<이슈번호>-<요약>` — 동작 변경 없는 리팩터링

### 작업 흐름

1. **GitHub Issue 생성** (라벨/마일스톤/Priority/Projects 필수 — §5 참조).
2. 이슈에서 브랜치 생성 (`Create a branch` 기능 활용 → 이슈 번호 자동 연결).
3. 개발 → 로컬 커밋.
4. PR 생성 → 본문에 `Closes #이슈번호` 명시 → 셀프 머지.
5. 머지 시 이슈 자동 클로즈 + 브랜치 삭제.

### 커밋 메시지 규약

```
<Prefix>: <50자 이내 요약>

<선택: 변경 이유 / 컨텍스트>
```

- **Prefix**: `Feat` / `Fix` / `Chore` / `Docs` / `Refactor` / `Test`
- 한글 사용 가능. 제목은 명령형/현재형.
- 큰 변경은 본문에 *왜* 그렇게 했는지 적기 (변경 내용은 diff가 말해줌).

### PR 본문 템플릿

```
## 변경 사항
- (불릿)

## 관련 이슈
Closes #이슈번호

## 체크리스트
- [ ] GDD와 충돌하지 않음
- [ ] OUT OF SCOPE 위반 없음
- [ ] 풀링 필요 객체는 풀링 적용
- [ ] 로컬에서 60fps 유지 확인 (해당 시)

## 씬 / 스크린샷
`테스트 가능한 씬 경로`
(UI/시각 변경 시 첨부)

## 비고
- (불릿)
```

---

## 5. 이슈 / Projects 관리 규칙

> **이슈를 만들 때는 반드시 다음 메타데이터를 함께 채운다.** 메타데이터가 없는 이슈는 Projects 보드에서 누락되어 백로그 가시성이 무너진다.

### 필수 메타데이터 (이슈 생성 시 항상)

| 항목 | 값 / 규칙 |
| --- | --- |
| **Title** | `[영역] 동사형 작업명` 예: `[Combat] 어뢰 컴포넌트 자동 사격 구현` |
| **Type** | GitHub Issue Type — `Feature` / `Bug` / `Task` 중 하나 (네이티브 필드, 라벨 아님) |
| **Labels** | 영역(area) 라벨 (아래 라벨 가이드 참조) — 최소 1개 |
| **Milestone** | `W1` / `W2` / `W3` 중 하나 (GDD §10 마일스톤과 1:1 매칭) |
| **Priority** | Projects 보드 `Priority` 필드 — `P0` / `P1` / `P2` (라벨 아님) |
| **Size** | Projects 보드 `Size` 필드 — `XS` / `S` / `M` / `L` / `XL` (작업량 추정) |
| **Projects** | `N0WST4NDUP's Mini Game` 보드(#40)에 추가 — `Status` 컬럼 자동 분류 |
| **Assignee** | 본인 (1인 개발이므로 항상) |

### 라벨 가이드

**영역 라벨** (택 1+):
- `area:combat` — 사격/데미지/투사체
- `area:movement` — 선박 이동/카메라
- `area:enemy` — 일반 몹/네임드/보스/AI
- `area:component` — 슬롯/컴포넌트/드롭 경쟁
- `area:ui` — HUD/메뉴/레벨업 카드/미니맵
- `area:vfx` — 이펙트/사운드
- `area:perf` — 부하/풀링/프로파일링
- `area:build` — 빌드/배포/CI
- `area:docs` — 문서

**상태/특수 라벨** (필요 시):
- `blocked` — 다른 이슈/외부 요인 대기
- `playtest-needed` — 플레이테스트로 수치 확정 필요
- `out-of-scope-candidate` — 스코프 검토 필요

> **종류·우선순위는 라벨이 아니다.** 종류는 GitHub 네이티브 Issue Type, 우선순위는 Projects 보드 필드로 관리한다 (아래 참조).

### Issue Type (네이티브 필드)

조직에 정의된 GitHub Issue Type 중 택 1 — 라벨이 아니라 이슈의 네이티브 `Type` 필드로 지정:
- `Feature` — 새 기능
- `Bug` — 버그
- `Task` — 기능 외 작업 (잡일/설정/리팩터링/조사·스파이크). 조사·스파이크는 본문에 시간 박스 명시.

### 우선순위 (Projects 보드 필드)

Projects 보드의 `Priority` 단일선택 필드로 관리 — 라벨 아님. 택 1:
- `P0` — 즉시 필수 (코어 루프/마일스톤 게이트)
- `P1` — 콘텐츠/마감 단계
- `P2` — 가용 시간 내 추가

### 작업량 추정 (Projects 보드 필드)

Projects 보드의 `Size` 단일선택 필드로 관리 — 라벨 아님. 택 1:
- `XS` / `S` / `M` / `L` / `XL` — 체감 작업량. 0.5일 이하 `XS`, 3일 초과 예상이면 `XL` 대신 이슈 분할 검토.

> 보드에는 `Team`(자동·고정값 `1인 개발자`), `Iteration`, `Estimate`, `Start date`, `Target date` 필드도 있으나 필수는 아니다 — 필요 시에만 사용.

### 이슈 본문 템플릿

```
## 목표
(한 줄, 무엇을 달성하면 close 가능한가)

## 컨텍스트 / 근거
- GDD §X.X
- (관련 결정/링크)

## 완료 기준 (DoD)
- [ ] (검증 가능한 항목)
- [ ] (검증 가능한 항목)

## 비고
(스코프 경계 / 미정 사항 / 의존 이슈)
```

### Projects 보드 컬럼 (`Status` 필드)

- `Todo` — 등록·대기 중 (백로그 + 다음 작업 후보)
- `In progress` — 진행 중 (동시 1~2개 권장, PR 오픈·셀프 리뷰 단계 포함)
- `Done` — 머지 완료

> 한눈에 백로그를 보려면 보드를 **Milestone(W1/W2/W3)** 또는 **Priority(P0/P1/P2)** 로 그룹핑한다.

---

## 6. 코드 스타일 (간단)

- C# 네이밍 컨벤션:
  - `PascalCase` — 타입(class/struct/enum/interface), 메서드, **public 멤버(필드/프로퍼티)**, `const`, `static readonly`
  - `camelCase` — 지역 변수, 메서드 파라미터
  - `_camelCase` — `private` / `protected` 필드 (직렬화된 `[SerializeField] private` 포함)
  - `I` prefix — 인터페이스 (`IDamageable`)
- `MonoBehaviour` 파일명 = 클래스명.
- 폴더 구조 규약은 [Docs/Architecture.md](Docs/Architecture.md) 작성 시 확정 (현재 placeholder).
- 매직 넘버는 ScriptableObject 또는 `const`로. 수치 직접 박지 않기.

---

## 7. Claude 작업 시 주의

- 코드 작성 전 GDD 관련 섹션을 먼저 읽고 SSoT 일치 확인.
- 이슈/PR 생성 작업을 위임할 때는 **§5의 메타데이터를 반드시 채워달라**고 명시.
- GDD에 `❓` 마커가 있는 값은 임의로 채우지 말고 플레이테스트 대상으로 남길 것.
- 새 시스템 추가 제안 시 GDD §9 OUT OF SCOPE 위반 여부 먼저 점검.
