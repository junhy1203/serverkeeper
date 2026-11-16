# ShutdownGuard — Windows 종료 방지 보조 도구

Windows 환경에서 **시스템 종료·전원 메뉴 동작을 감지하고 확인 절차를 제공하도록 설계된 .NET 데스크톱 도구**입니다. WPF 프로그램과 백그라운드 감시 서비스, 설치 프로그램으로 구성됩니다.

## 프로젝트 구성

| 경로 | 역할 |
| --- | --- |
| `ShutdownGuard/` | WPF 본체, 종료 확인창, 트레이, 전원 메뉴 감시 및 Windows 이벤트 처리 |
| `ShutdownGuardService/` | Windows 백그라운드 감시 서비스. 본체 프로세스를 주기적으로 확인하고 재시작 시도 |
| `ShutdownGuardInstaller/` | 설치 및 실행 파일 배포용 프로젝트 |
| `ShutdownGuard.sln` | Visual Studio 솔루션 |
| `build.ps1` | Windows x64용 빌드·패키징 스크립트 |

Windows UI Automation, WMI, 전역 입력 후크, 레지스트리 설정 등 운영체제 기능을 사용합니다.

## 개발 및 빌드

요구 환경: **Windows, .NET 8 SDK**, PowerShell.

```powershell
dotnet restore ShutdownGuard.sln
dotnet build ShutdownGuard.sln -c Release
```

패키지 구성까지 수행하려면 저장소 루트에서:

```powershell
.\build.ps1
```

스크립트는 Windows x64 런타임 대상 프로그램과 서비스를 게시한 다음 설치 프로그램 산출물을 생성하도록 작성되어 있습니다. 실행 전 설치 경로·관리자 권한·서비스 등록 동작을 검토하세요.

## 주의사항

- Windows의 모든 종료 경로를 완전히 차단하는 보안 솔루션이 아닙니다. 강제 종료, 전원 차단, 업데이트·시스템 권한에 따른 동작은 별개입니다.
- 전역 후크·서비스 및 종료 흐름 변경은 시스템 동작에 영향을 줄 수 있으므로 개인 테스트 환경에서 검증하세요.
- 관리되지 않은 PC나 조직 정책이 적용된 컴퓨터에는 승인 없이 설치하지 마세요.
