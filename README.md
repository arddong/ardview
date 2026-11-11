# ArdView

광고 없는 무료 이미지 보기 (Windows 10·11 64비트)

- 열기: jpg · png · gif · webp · bmp · tiff · ico (움직이는 gif·webp 재생)
- 빠른 넘기기: 앞뒤 그림을 미리 읽어 둠 · 탐색기와 같은 이름 순서
- 휠 = 다음·이전 그림 · Ctrl+휠 = 커서 기준 확대 · 끌어서 이동
- 사진의 회전 정보(EXIF)대로 바로 세워 보여 줌
- 전체 화면 · 돌리기 · 그림 복사 · 휴지통으로 · 폴더에서 보기

다운로드: [Releases](https://github.com/arddong/ardview/releases/latest) · 소개 페이지: https://arddong.github.io/ardview/

## 단축키

| 키 | 하는 일 |
|---|---|
| ← → · 휠 · PgUp PgDn · Space | 이전 · 다음 그림 |
| Home End | 처음 · 마지막 그림 |
| Ctrl+휠 · + - | 확대 · 축소 |
| 0 · 1 | 화면에 맞추기 · 원래 크기 |
| Enter · F11 · 두 번 누르기 | 전체 화면 |
| R · L | 오른쪽 · 왼쪽으로 돌리기 (파일은 그대로) |
| Ctrl+C · Ctrl+E · Delete | 그림 복사 · 폴더에서 보기 · 휴지통으로 |
| Esc | 전체 화면 끄기 · 닫기 |

## 빌드

```
dotnet publish src -c Release -o publish
ISCC.exe ArdView.iss
```

시험: `python test/make_samples.py` 후 `dotnet run --project test`

## 라이선스

MIT. 쓴 오픈소스는 [THIRD_PARTY_NOTICES](docs/THIRD_PARTY_NOTICES.txt) 참고.
