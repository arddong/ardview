# ArdView

광고 없는 무료 이미지 보기 (Windows 10·11 64비트). 꿀뷰와 같은 단축키로 씁니다.

다운로드: [Releases](https://github.com/arddong/ardview/releases/latest) · 소개 페이지: https://arddong.github.io/ardview/

## 1. 할 수 있는 것

- **빠른 넘기기** = 앞뒤 그림을 미리 읽어 둠 · 탐색기와 같은 이름 순서 · `]` `[` 로 다음·이전 폴더
- **확대·축소** = 아래 도구줄 버튼 · `+` `-` · Ctrl+휠(커서 자리 기준) · 배율 눌러 고르기 · 확대하면 미리보기 창
- **사진 정보 (Tab)** = 히스토그램 · 파일 정보 · EXIF(카메라·렌즈·셔터·조리개·ISO·초점 거리·찍은 때) · GPS 를 지도로
- **압축 파일 안 그림** = zip · cbz · rar · cbr · 7z · tar 를 풀지 않고 봄
- **두 쪽 보기** = 만화처럼 나란히 (왼→오 · 왼←오)
- **슬라이드 쇼 · 책갈피 · 사진 폴더(Insert 로 복사·옮기기)**
- **변환 (Ctrl+T)** = jpg · png · webp · bmp 로, 크기 줄여서, 한 장 또는 목록 전체
- 돌리기 · 뒤집기 · 색 뒤집기 · 감마 (파일은 그대로) · 인쇄 · 바탕 화면으로 · 휴지통으로

## 2. 여는 형식

| 종류 | 형식 |
|---|---|
| 그림 | jpg · png · gif · webp · bmp · tiff(여러 쪽) · ico · psd · tga · pnm |
| 움직이는 그림 | gif · webp |
| 윈도우 디코더 | jxr · dds · heic · avif (heic·avif 는 윈도우 확장이 있을 때) |
| RAW | dng · cr2 · cr3 · nef · arw · raf · orf · rw2 · pef 등 = 안에 든 미리보기 그림 |
| 압축 파일 | zip · cbz · rar · cbr · 7z · cb7 · tar |

## 3. 단축키

프로그램 안에서 `F1` 을 누르면 전체 목록이 나옵니다. 오른쪽 클릭 메뉴에도 모두 있습니다.

| 키 | 하는 일 |
|---|---|
| ← → · 휠 · PgUp PgDn · Space | 이전 · 다음 그림 |
| `+` `-` · Ctrl+휠 | 확대 · 축소 |
| `0` · `9` · `8` | 100% · 창에 맞추기 · 폭에 맞추기 |
| Tab | 사진 정보·EXIF |
| Alt+Enter · N · F11 | 전체 화면 |
| `7` · `6` | 두 쪽 보기 |
| `]` · `[` | 다음 · 이전 폴더나 압축 파일 |
| Ctrl+1~9 · Ctrl+0 | 슬라이드 쇼 · 멈춤 |
| Ctrl+T | 변환 |
| Delete · Insert | 휴지통으로 · 사진 폴더로 복사 |

휠로 확대·축소하고 싶으면 `F5` 설정에서 「휠 = 확대·축소」를 켭니다.

## 4. 빌드

```
dotnet publish src -c Release -o publish
ISCC.exe ArdView.iss
```

시험: `python test/make_samples.py` 후 `dotnet run --project test`

## 5. 라이선스

MIT. 쓴 오픈소스는 [THIRD_PARTY_NOTICES](docs/THIRD_PARTY_NOTICES.txt) 참고.
