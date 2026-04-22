## **0. 핵심 지침 (Core Principles)**
* **MainScene 보호**: 핵심 시스템 구현 단계이므로 `MainScene`은 수정하지 않고 비워둡니다.
* **개인 작업 환경**: 각 작업자는 자신의 이름으로 된 전용 씬을 생성하여 테스트를 진행합니다. (예: `JaeinScene`)
* **씬 관리**: 모든 씬 파일(.unity)은 `00.Scenes` 폴더 내에 저장합니다.
* **버전 정보**: 프로젝트는 **Unity 6.0.3f1** 버전을 사용합니다.

## **1. 폴더 구조 (Folder Structure)**
프로젝트 뷰의 가독성과 정렬을 위해 아래의 인덱싱 구조를 엄격히 따릅니다.

* **00.Scenes**: 모든 씬 파일 (`.unity`)
* **01.Scripts**: 모든 C# 스크립트
* **02.Prefabs**: 재사용 가능한 프리팹
* **03.Materials**: 메테리얼, 텍스처 및 셰이더 관련 에셋
* **04.Art**: 3D 모델 및 원형 소스 에셋
* **05.UI**: UI 이미지, 폰트 및 UI 전용 프리팹
* **06.Audio**: SFX 및 BGM 오디오 파일
* **07.VFX**: 이펙트 및 파티클 시스템
* **08.Data**: ScriptableObject, JSON, CSV 및 데이터 클래스
* **09.Plugins**: 외부 라이브러리 및 SDK (Cinemachine, Input System 등)
* **98.Debugger**: 디버깅용 툴 및 디버그 전용 스크립트
* **99.Test**: 임시 테스트용 스크립트 및 리소스

---

## **2. Git 브랜치 전략 (GitFlow)**
* **main**: 최종 배포 및 빌드용 브랜치.
* **dev**: 개발 통합 브랜치. 모든 기능 구현 결과가 모이는 중심.
* **feature/**: 단위 기능 구현 브랜치. (예: `feature/player-movement`)

## **3. 코드 컨벤션 (C# Naming Convention)**
* **PascalCase**: 클래스(Class), 메서드(Method), 프로퍼티(Property)
* **_camelCase**: `private` 및 `protected` 필드. 접두어 언더바(`_`) 사용 필수.
* **camelCase**: 지역 변수(Local Variable), 파라미터(Parameter)

## **4. 프로그래밍 규칙 (Programming Rules)**

### **싱글톤 및 매니저 참조 규칙 (Singleton & Registry)**
* **최우선 초기화**: 모든 싱글톤 클래스 상단에 `[DefaultExecutionOrder(-100)]`을 선언합니다.
* **인스턴스 관리**: `Awake`에서 `Instance` 할당, `DontDestroyOnLoad` 설정, `ManagerRegistry` 등록을 수행합니다.
* **참조 방식**: 타 매니저 참조 시 `ManagerRegistry.Get<T>()` 또는 `TryGet<T>()` 사용을 우선합니다.
* **이벤트 생명주기**: 이벤트 구독은 `OnEnable`, 해제는 `OnDisable`에서 수행하여 메모리 누수를 방지합니다.

### **엄격한 예외 처리 (Strict Null Check)**
* **에러 로그 강제**: 참조 확인 시 단순 `return` 처리를 금지합니다. 반드시 `Debug.LogError()`를 호출하여 콘솔에 에러를 명시하고 로직을 즉시 중단합니다.

---

## **5. 핵심 코드 구조 (Standard Code)**

```csharp
// 01.Scripts/Core/Singleton.cs
[DefaultExecutionOrder(-100)]
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    [SerializeField] private bool _isDontDestroyOnLoad = true;
    private static T _instance;
    public static T Instance 
    {
        get {
            if (_instance == null) Debug.LogError($"{typeof(T).Name} 인스턴스가 씬에 존재하지 않습니다.");
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this as T;
        if (_isDontDestroyOnLoad) DontDestroyOnLoad(gameObject);
        
        ManagerRegistry.Register<T>(_instance);
        OnInitialized();
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    protected virtual void OnInitialized() { }
}
```

---

## **6. 작업 흐름 요약**
1. `dev`에서 `feature/기능-이름` 브랜치 생성.
2. 자신의 이름으로 된 테스트 씬에서 기능을 구현.
3. 머지 전 `dev`를 자신의 브랜치로 가져와(Pull/Merge) 충돌 해결.
4. 컴파일 에러 및 로그 상 결함이 없는 상태로 `dev`에 병합.
