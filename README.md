# City Flow

簡易3D都市に有向Lineを配線し、色付きFLOWを対応するSinkへ運ぶUnityゲーム。
まずは [仕様書](City-Flow-Specification.md) のv0.1（Ground配線MVP）を開発する。

## 開発環境

| 項目 | 固定バージョン |
| --- | --- |
| Unity | 6.4 / **6000.4.7f1** |
| URP / Shader Graph / VFX Graph | 17.4.0 |
| Input System | 1.19.0 |
| Cinemachine | 3.1.7 |
| UniTask | 2.5.11 |
| R3 / R3.Unity | 1.3.1 |
| VContainer | 1.19.0 |
| Unity Test Framework | 1.6.0 |
| NuGetForUnity / CLI | 4.5.0 |
| uloop CLI / Unity CLI Loop | 3.5.1 / 3.6.3 |

Unity本体と同梱のURPテンプレートを基に構成。Git、初回の依存取得用ネットワーク、CLI復元を使う場合は.NET SDKも必要。

## 初回セットアップ

1. Unity Hubで **6000.4.7f1** をインストールする。
2. リポジトリのルートで以下を実行し、R3本体と依存DLLを復元する。

   ```sh
   dotnet tool restore
   dotnet nugetforunity restore CityFlow
   ```

3. `uloop launch CityFlow` で、`ProjectVersion.txt` と一致するUnity Editorを起動する。
4. 初回インポート後、`uloop --project-path CityFlow list` でUnity CLI Loopとの接続を確認する。
5. `uloop --project-path CityFlow control-play-mode --action Play` で起動する。固定の簡易都市・5 Node・5 Lineを読み込み、Sourceから赤／青FLOWが生成・移動し、同色Sinkで消化される。HUDでBuffer・In-Flight・成功数・接続数を確認できる。設定は `Assets/CityFlow/Settings/Gameplay/` の2アセットで調整する。

R3は [公式のUnity導入手順](https://github.com/Cysharp/R3#unity) に従い、NuGetのコアとUPMのUnity連携を併用している。
Unityで開く前に [NuGetForUnity CLI](https://github.com/GlitchEnzo/NuGetForUnity#restoring-nuget-packages-over-the-command-line) で復元すると、初回のDLL不足によるコンパイル失敗を避けられる。
Editor内では `NuGet > Restore Packages` でも復元可能。
`CityFlow/Assets/packages.config` の変更時は復元を再実行する。復元された `CityFlow/Assets/Packages` はGit管理対象外。

## 構成

```text
CityFlow/              # Unityプロジェクトルート
  Assets/CityFlow/
    Runtime/
      Domain/          # ゲームルール・集約・値オブジェクト
        FlowNetwork/   # Node、Line、FLOW、Buffer、局所Routing
        Spatial/       # 経路の値・Ground制約・距離
        Progression/   # Wave、生成、Overload、生存時間
      Application/     # ユースケース・シミュレーション進行・外部ポート
      Infrastructure/  # Unity空間判定・設定読み込み・将来のPLATEAU連携
      Presentation/    # Input、Cinemachine、UI、Line描画、VFX
      Composition/     # VContainer LifetimeScope
    Editor/            # Editor専用ツール
    Tests/
      EditMode/        # ルール・ユースケース・プロジェクト設定
      PlayMode/        # 起動シーン・PlayerLoop・Unity連携
    Scenes/            # Bootstrap（同一都市シーン内でWaveを進行）
    Settings/          # Rendering、Gameplay
    Prefabs/
    Art/               # Materials、Shaders、VFX
  Packages/            # UPM manifest / lock
  ProjectSettings/     # Unityの共有設定
docs/                 # 設計・テスト方針
```

現在はIssue #1〜#3の検証都市、FlowNetwork集約、基本輸送まで実装。Unity EditorのBootstrapシーンで確認できる。配線操作、Overload敗北、Waveは後続の実装対象。空のモジュールは配置先だけを用意している。

## 開発と検証

- [AGENTS.md](AGENTS.md)：開発規約。コードコメントは英語、コミットメッセージは日本語。
- [Architecture.md](docs/Architecture.md)：DDDの境界、依存方向、PLATEAU拡張方針。
- [Testing.md](docs/Testing.md)：TDD、仕様に対応するテスト候補、実行方法。

```sh
uloop --project-path CityFlow compile
uloop --project-path CityFlow run-tests --test-mode EditMode
uloop --project-path CityFlow run-tests --test-mode PlayMode
uloop --project-path CityFlow get-logs
```

Unity Editorに対する操作はすべてuloop経由で行う。Unity実行ファイルやバッチモードを直接呼び出さない。
利用可能なコマンドと引数は `uloop --project-path CityFlow list` および各コマンドの `--help` で確認する。
描画の見た目・実入力・VFXの品質は、uloopの `screenshot` と入力シミュレーションを使って確認する。

## 拡張順序

- **v0.1**：簡易3D都市、Ground配線、固定Line容量、接続本数制。
- **v0.2**：高さ方向、Port Unit、Width、複数経路候補、方向反転。
- **v0.3**：PLATEAU SDKによる実在都市の取り込み。簡易都市も比較用に残す。

PLATEAU SDKはまだ導入していない。対象都市・詳細度・性能目標、Node追加のレベルデザイン方式は未決定。
