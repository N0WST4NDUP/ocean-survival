# README + WebGL 배포/임베드 설계

- **작성일**: 2026-09-08
- **상태**: 승인됨 (구현 계획 대기)
- **대상 저장소**: `N0WST4NDUP/ocean-survival` (조직 `Devel-Rocket-ClassRoom`에서 이관 완료)

---

## 1. 목표

1인 개발 미니게임 ShipSurivor의 포트폴리오용 README를 작성하고, WebGL 빌드를 GitHub
Pages에 배포해 브라우저에서 바로 플레이할 수 있게 한다.

완료 조건:

- `https://n0wst4ndup.github.io/ocean-survival/` 에서 게임이 로딩되고 플레이된다.
- README 최상단의 `▶ PLAY IN BROWSER` 배너가 위 주소로 연결된다.
- README에 GIF 4개와 스크린샷 3개가 표시된다.

---

## 2. 전제와 제약

### 2.1 GitHub README는 iframe을 렌더링하지 않는다

GitHub은 마크다운 안의 `<iframe>`과 `<script>`를 무조건 제거한다. 따라서 github.com의
README **안에서** 직접 플레이하게 만드는 것은 불가능하다. 우회 없음.

대신 두 가지를 조합한다.

1. README: 자동재생 GIF + 클릭 가능한 `▶ PLAY` 배너 이미지
2. GitHub Pages 랜딩 페이지: 여기서 `<iframe>`으로 실제 임베드

### 2.2 Firebase Unity SDK는 WebGL을 지원하지 않는다

프로젝트에 Firebase 13.16.0 (Auth + Realtime Database)이 설치되어 있고, 게임 스크립트
5개가 사용 중이다. 네이티브 라이브러리는 `Assets/Firebase/Plugins/x86_64/`에
`.dll`(Windows) / `.bundle`(macOS) / `.so`(Linux)와 Android `m2repository`만 존재하며,
wasm 빌드는 없다.

Firebase Unity SDK는 C++ 네이티브 코어에 P/Invoke하는 래퍼다. 네이티브 바이너리가 없는
플랫폼에서는 원리상 동작할 수 없다. 현재 상태로 WebGL 빌드 시 관리 어셈블리는 컴파일을
통과하지만 런타임에 `DllNotFoundException: FirebaseCppApp-13_16_0`으로 중단된다.

현재 코드베이스에 `UNITY_WEBGL` 전처리 가드는 0개다.

### 2.3 GitHub Pages는 Content-Encoding 헤더를 붙이지 않는다

현재 설정은 `webGLCompressionFormat: 0`(Brotli), `webGLDecompressionFallback: 0`(OFF)이다.
이 조합으로 빌드해 Pages에 올리면 로딩 바 0%에서
`Unable to parse Build/*.framework.js.br` 오류로 실패한다.

### 2.4 LFS 대역폭

`.gitattributes`가 `*.png`, `*.gif`, `*.jpg`, `*.jpeg`, `*.mp4`를 전부 LFS로 추적한다.
README 미디어를 그대로 넣으면 조회 때마다 LFS 대역폭(무료 1GB/월)을 소모하고, 초과 시
이미지가 통째로 깨진다. 포트폴리오에서 가장 치명적인 실패 모드이므로 회피한다.

---

## 3. 결정 사항

| # | 결정 | 근거 |
|---|---|---|
| D1 | 로컬 빌드 + 수동 배포 (GitHub Actions 아님) | game-ci는 `UNITY_LICENSE` 활성화에 초기 2~4시간, CI 빌드마다 20~40분 대기. 1인 프로젝트의 배포 빈도에 비해 과함 |
| D2 | 호스팅은 GitHub Pages (`gh-pages` orphan 브랜치) | itch.io는 `X-Frame-Options`로 외부 iframe 임베드를 차단한다. 랜딩 페이지 임베드가 목표이므로 Pages가 강제됨 |
| D3 | WebGL에서 Firebase는 `#if UNITY_WEBGL` 가드 + 로컬 폴백 | JS SDK 브릿지는 2~4일 + API 키 노출 대응 필요. 데모의 목적은 "만져보게 하는 것"이고 온라인 리더보드는 GIF로 보여주면 충분 |
| D4 | GIF는 기존 플레이 영상에서 추출 | README를 먼저 확정해 필요한 GIF를 명세하고, 사용자가 타임스탬프를 지정한 뒤 ffmpeg로 가공 |

---

## 4. 아키텍처

### 4.1 브랜치 구조

```
main                              gh-pages (orphan)
├─ Assets/                        ├─ index.html          랜딩 (iframe 임베드)
├─ Docs/                          └─ game/               Unity WebGL 산출물
│  ├─ GDD.md                         ├─ index.html
│  ├─ Balancing.md                   ├─ Build/
│  ├─ Boss/PirateLord.md             └─ TemplateData/
│  ├─ images/     README 미디어
│  └─ superpowers/specs/
└─ README.md
```

- `main`은 소스만 보유한다. 빌드 산출물이 히스토리에 쌓이지 않는다.
- `gh-pages`는 orphan 브랜치이며 배포 시 force push한다. 이전 빌드를 누적하지 않으므로
  저장소가 비대해지지 않는다.
- README 미디어는 `main`의 `Docs/images/`에 둔다. Pages 가용성과 무관하게 README가
  자기완결적으로 렌더링된다.

### 4.2 LFS 예외

`.gitattributes` 최하단에 추가한다.

```gitattributes
# README 미디어는 LFS 대역폭을 소모하지 않도록 일반 git 객체로 저장
/Docs/images/**         filter= diff= merge= -text
```

미디어 총량 예산: **10MB 이하** (8.2절 슬롯 목표 합계 9.4MB). 초과 시 GIF 해상도 또는
길이를 줄인다.

---

## 5. Firebase WebGL 분리

### 5.1 방식

매니저 5개 각각을 파일 상단 `#if UNITY_WEBGL` / `#else` / `#endif`로 분기하고, WebGL
분기에 **동일한 public API를 갖는 로컬 폴백 클래스**를 둔다. 클래스명·메서드
시그니처·이벤트가 동일하므로 호출부는 수정하지 않는다.

`using Firebase;` 및 Firebase 타입을 쓰는 코드 전체가 `#else` 분기 안에 들어가야 WebGL
컴파일이 통과한다.

`#if UNITY_WEBGL`은 빌드 타깃이 WebGL이면 에디터에서도 참이므로, Play 모드로 폴백 동작을
검증할 수 있다.

### 5.2 대상 파일과 폴백 동작

| 파일 | 줄 수 | WebGL 폴백 |
|---|---|---|
| `Assets/Scripts/Core/Firebase/FirebaseInitializer.cs` | 120 | 즉시 Ready 이벤트 발행 |
| `Assets/Scripts/Core/Firebase/Auth/AuthManager.cs` | 252 | 게스트 로그인. 닉네임은 `PlayerPrefs` |
| `Assets/Scripts/Core/Firebase/Profile/ProfileManager.cs` | 186 | 로컬 프로필 (`PlayerPrefs`) |
| `Assets/Scripts/Core/Firebase/Record/RecordManager.cs` | 204 | 베스트 클리어 타임 `PlayerPrefs` |
| `Assets/Scripts/Core/Firebase/Leaderboard/LeaderboardManager.cs` | 208 | 로컬 기록 상위 10개만 반환 |

데이터 전용 타입 `LeaderboardEntry.cs`, `UserProfileData.cs`, `ClearTimeRecord.cs`,
`TimeUtil.cs`는 Firebase 타입에 의존하지 않으면 수정 대상이 아니다. 구현 시 확인한다.

### 5.3 수정하지 않는 호출부

아래 7개 파일은 한 줄도 변경하지 않는다. 변경이 필요해지면 5.1의 전제가 깨진 것이므로
설계를 재검토한다.

- `Assets/Scripts/Core/Singleton/GameManager(ClearTime).cs`
- `Assets/Scripts/UI/GameResult/GameResultUI.cs`
- `Assets/Scripts/UI/Leaderboard/LeaderboardUI.cs`
- `Assets/Scripts/UI/Title/LoginUI.cs`
- `Assets/Scripts/UI/Title/Logout.cs`
- `Assets/Scripts/UI/Title/TitleUI.cs`
- `Assets/Scripts/Test/LeaderboardTestRecorder.cs`

### 5.4 왜 인터페이스 추출이 아닌가

`IAuthService` / `IRecordService` / `ILeaderboardService`를 추출하고 부트스트랩에서 구현을
주입하는 편이 구조적으로는 낫다. 그러나 프로젝트는 v1.0.0으로 이미 기능이 완성된 상태이며
Firebase 레이어가 앞으로 크게 변할 가능성이 낮다. 154개 스크립트 중 7개 호출부를 건드리는
리팩터링보다, 폴백 약 180줄을 추가하는 쪽이 회귀 위험이 낮다.

향후 Firebase 레이어를 확장하게 되면 그 시점에 인터페이스 추출을 별도 작업으로 다룬다.

---

## 6. WebGL 빌드 설정

전부 에디터 작업이다.

```
Edit > Project Settings > Player > WebGL 탭 > Publishing Settings
  Compression Format      = Brotli
  Decompression Fallback  = ON        (현재 OFF — 반드시 변경)

Edit > Project Settings > Player > WebGL 탭 > Other Settings
  Strip Engine Code       = ON

File > Build Profiles > WebGL > Switch Platform
  Scene List: Assets/Scenes/EntryPoint.unity, Assets/Scenes/InGame.unity  (현재와 동일)
  출력 경로: <스크래치 폴더>/webgl-build   (저장소 밖. .gitignore가 Build/를 제외하므로 혼동 방지)
```

Decompression Fallback을 켜면 로더에 JS 압축 해제기가 포함되어 초기 로딩이 다소 느려지고
로더 크기가 약 100KB 증가한다. Pages에서 동작하게 만드는 유일한 방법이므로 수용한다.

WebGL로 플랫폼을 처음 전환하면 에셋 재임포트에 시간이 걸리고, 첫 IL2CPP 빌드는 20~40분
걸린다.

---

## 7. 랜딩 페이지 (`gh-pages/index.html`)

정적 HTML 한 장. 프레임워크·빌드 도구 없음.

- 상단: 게임 타이틀, 한 줄 소개, GitHub 저장소 링크
- 중앙: `<iframe src="./game/index.html">` — 16:9 고정 비율, 최대 폭 960px
- 하단: 조작법 표, "온라인 리더보드는 PC 빌드에서 지원" 안내 한 줄
- 반응형: 좁은 화면에서 iframe이 가로 스크롤을 만들지 않도록 `width: 100%` + `aspect-ratio`
- iframe에 `allow="autoplay; fullscreen"` 부여

Unity WebGL 빌드 결과의 `index.html`은 그대로 `game/`에 넣고 수정하지 않는다.

### 7.1 배포 절차

`main`의 워킹트리를 건드리지 않도록 별도 워크트리에서 수행한다. 매 배포는 orphan 브랜치를
새로 만들어 force push하므로 이전 빌드가 누적되지 않는다.

1. 스크래치 폴더에 WebGL 빌드를 출력한다 (6절).
2. `git worktree add --detach <스크래치>/pages` 로 배포용 워크트리를 만든다.
3. 그 워크트리에서 `git checkout --orphan gh-pages && git rm -rf .` 로 비운다.
4. 랜딩 `index.html`을 루트에, 빌드 산출물을 `game/`에 복사한다.
5. `.nojekyll` 빈 파일을 루트에 추가한다. **Jekyll이 밑줄로 시작하는 파일과 폴더를 무시하기
   때문에** 이것 없이는 Unity 산출물 일부가 404가 날 수 있다.
6. 커밋 후 `git push -f origin gh-pages`.
7. 워크트리를 제거한다 (`git worktree remove`).
8. 최초 1회: 저장소 Settings > Pages > Source = `Deploy from a branch`, Branch = `gh-pages` / `/ (root)`.

`gh-pages` 브랜치에는 `.gitattributes`가 없으므로 빌드 산출물이 LFS를 타지 않는다.
3단계의 `git rm -rf .` 는 **해당 워크트리 안에서만** 동작하며 `main` 워킹트리에 영향이 없다.
실행 전 현재 경로가 배포용 워크트리인지 반드시 확인한다.

---

## 8. README 구조

### 8.1 섹션 순서

1. **히어로** — 타이틀 이미지, 배지(Unity 버전 / 라이선스 / 플랫폼), `▶ PLAY IN BROWSER` 배너 링크
2. **한 줄 소개 + G1** — 장르와 핵심 재미
3. **조작법** — 표
4. **주요 시스템** — 컴포넌트 슬롯(G2), 레벨업(G3), 보스전(G4), 리더보드(S2)
5. **기술 스택** — Unity 6 URP, Input System, Cinemachine, Behavior, Firebase
6. **빌드 & 실행** — PC 빌드 다운로드, 로컬 실행 방법
7. **문서** — `Docs/GDD.md`, `Docs/Balancing.md`, `Docs/Boss/PirateLord.md` 링크

`▶ PLAY IN BROWSER` 배너는 이미지 링크로 구현한다.

```markdown
[![Play in browser](Docs/images/play-banner.png)](https://n0wst4ndup.github.io/ocean-survival/)
```

### 8.2 미디어 슬롯 명세

| ID | 파일명 | 위치 | 담아야 할 내용 | 길이 | 목표 용량 |
|---|---|---|---|---|---|
| G1 | `hero.gif` | 히어로 직하 | 다수의 적 한복판에서의 전투. 게임 정체성을 한 컷에 | 6~8초 | ≤ 2.5MB |
| G2 | `component.gif` | 주요 시스템 | 드롭 획득 → 슬롯 장착 → 화력 변화 | 5~6초 | ≤ 2MB |
| G3 | `levelup.gif` | 주요 시스템 | 레벨업 카드 3장 중 선택 → 즉시 반영 | 4~5초 | ≤ 1.5MB |
| G4 | `boss.gif` | 주요 시스템 | Pirate Lord 페이즈 전환 | 6~8초 | ≤ 2.5MB |
| S1 | `title.png` | 히어로 | 타이틀 화면 | — | ≤ 300KB |
| S2 | `leaderboard.png` | 주요 시스템 | 리더보드 화면 | — | ≤ 300KB |
| S3 | `result.png` | 주요 시스템 | 결과 화면 (점수 count-up 후) | — | ≤ 300KB |

README는 위 파일들을 참조하는 상태로 먼저 작성하고, 미디어는 나중에 채운다. 미디어가 없는
동안 README는 깨진 이미지로 보이므로, 미디어 확보 전까지는 `main`에 머지하지 않는다.

---

## 9. 미디어 파이프라인

1. 사용자가 기존 플레이 영상에서 각 슬롯에 해당하는 **시작 타임스탬프와 길이**를 지정한다.
2. ffmpeg를 설치한다 (`winget install Gyan.FFmpeg`).
3. 슬롯별로 팔레트 최적화 GIF를 생성한다.

```bash
ffmpeg -ss <시작> -t <길이> -i <입력.mp4> \
  -vf "fps=12,scale=640:-1:flags=lanczos,split[s0][s1];[s0]palettegen=max_colors=128[p];[s1][p]paletteuse=dither=bayer:bayer_scale=3" \
  -loop 0 Docs/images/<이름>.gif
```

4. 목표 용량 초과 시 순서대로 조정한다: `fps` 12 → 10, `scale` 640 → 560, 길이 단축.
5. 스크린샷은 같은 영상에서 단일 프레임으로 추출한다.

```bash
ffmpeg -ss <시각> -i <입력.mp4> -frames:v 1 Docs/images/<이름>.png
```

### 9.1 대안 (용량이 문제가 될 경우)

GitHub은 자체 asset CDN(`user-attachments`)에 올린 `.mp4`를 README에서 인라인 플레이어로
렌더링한다. 이슈 코멘트 작성 창에 mp4를 드래그해 얻은 URL을 README에 붙이는 방식이다.
화질 대비 용량이 GIF보다 훨씬 유리하지만 **자동재생되지 않고 클릭이 필요**하다. 히어로
슬롯(G1)은 자동재생이 중요하므로 GIF를 유지하고, 나머지 슬롯에서만 검토한다.

---

## 10. 검증

| 대상 | 방법 |
|---|---|
| Firebase 폴백 | 에디터를 WebGL 타깃으로 전환 후 Play 모드. 로그인 화면을 건너뛰고 게임이 시작되며, 클리어 후 베스트 타임이 저장·표시되는지 확인 |
| PC 빌드 회귀 | 플랫폼을 Windows로 되돌린 뒤 Play 모드. Firebase 로그인·리더보드가 종전대로 동작하는지 확인 |
| WebGL 빌드 로딩 | 배포 후 `https://n0wst4ndup.github.io/ocean-survival/game/`를 브라우저에서 열어 로딩 바가 100%까지 도달하는지 확인. 콘솔에 `Unable to parse` 오류가 없을 것 |
| 랜딩 페이지 임베드 | `https://n0wst4ndup.github.io/ocean-survival/`에서 iframe 안의 게임이 조작에 반응하는지 확인 |
| README 렌더링 | GitHub에서 이미지 7개가 모두 표시되고 `▶ PLAY` 배너 링크가 동작하는지 확인 |
| LFS 예외 | `git check-attr filter -- Docs/images/hero.gif`가 `filter: unspecified`를 반환할 것 |

---

## 11. 범위 밖

- GitHub Actions 자동 빌드 (D1에서 기각. 추후 별도 작업)
- Firebase JS SDK 브릿지 및 WebGL 온라인 리더보드 (D3에서 기각)
- itch.io 동시 배포
- 모바일 터치 조작 대응
- Firebase 레이어의 인터페이스 추출 리팩터링 (5.4)
- 커스텀 WebGL 템플릿 (`webGLTemplate`은 `Default` 유지)

---

## 12. 리스크

| 리스크 | 영향 | 대응 |
|---|---|---|
| WebGL 빌드가 Firebase 외의 이유로 실패 | 일정 지연 | 첫 빌드를 가급적 빨리 돌려 문제를 조기에 노출시킨다 |
| IL2CPP 빌드 산출물이 Pages 용량에 비해 과대 | 로딩 지연 | Strip Engine Code + Brotli로 대응. 그래도 크면 `Imported/Epic Toon FX` 데모 씬을 빌드에서 제외 검토 |
| 폴백 전환 후 PC 빌드 회귀 | 기존 기능 손상 | 10절의 PC 빌드 회귀 검증을 매 커밋 후 수행 |
| 저장소 이름 변경 시 Pages URL 및 README 링크 파손 | README 배너 무효 | 이름을 확정하고 변경하지 않는다 |

---

## 13. 참고

- 프로젝트 규약: [CLAUDE.md](../../../CLAUDE.md)
- 게임 사양: [Docs/GDD.md](../../GDD.md)
- 보스 사양: [Docs/Boss/PirateLord.md](../../Boss/PirateLord.md)
