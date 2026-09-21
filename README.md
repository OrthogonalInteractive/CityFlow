# City Flow

簡易3D都市に有向Lineを配線し、色付きFLOWを対応するSinkへ運ぶUnityゲーム。
まずは [仕様書](City-Flow-Specification.md) のv0.1（Ground配線MVP）を開発する。

## 開発環境

| 項目 | 固定バージョン |
| --- | --- |
| Unity | 6.4 / **6000.4.7f1** |
| URP / Shader Graph / VFX Graph | 17.4.0 |
| UI Toolkit | Unity 6.4同梱 |
| Input System | 1.19.0 |
| Cinemachine | 3.1.7 |
| UniTask | 2.5.11 |
| R3 / R3.Unity | 1.3.1 |
| VContainer | 1.19.0 |
| Unity Test Framework | 1.6.0 |
| Visual Studio Editor（VS Code連携・C#プロジェクト生成） | 2.0.27 |
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
5. `uloop --project-path CityFlow control-play-mode --action Play` で起動する。固定の簡易都市・5 Node・5 Lineを読み込み、Sourceから赤／青FLOWが生成・移動し、同色Sinkで消化される。UI ToolkitのHUDでBuffer・In-Flight・成功数・接続数を確認できる。設定は `Assets/CityFlow/Settings/Gameplay/` の2アセットで調整する。

R3は [公式のUnity導入手順](https://github.com/Cysharp/R3#unity) に従い、NuGetのコアとUPMのUnity連携を併用している。
Unityで開く前に [NuGetForUnity CLI](https://github.com/GlitchEnzo/NuGetForUnity#restoring-nuget-packages-over-the-command-line) で復元すると、初回のDLL不足によるコンパイル失敗を避けられる。
Editor内では `NuGet > Restore Packages` でも復元可能。
`CityFlow/Assets/packages.config` の変更時は復元を再実行する。復元された `CityFlow/Assets/Packages` はGit管理対象外。

### VS CodeとC#プロジェクトファイル

VS Codeではリポジトリ直下の `CityFlow.code-workspace` を開く。このワークスペースはUnityプロジェクトの `CityFlow/` をルートとし、`CityFlow.slnx` を使用する。

```sh
code CityFlow.code-workspace
```

推奨拡張の **Unity**（Microsoft / `visualstudiotoolsforunity.vstuc`）をインストールする。C# Dev KitとC#拡張も依存として導入される。
Unity側は **Visual Studio Editor**（`com.unity.ide.visualstudio`）2.0.27を導入済み。VS Codeもこのパッケージで連携する。[公式手順](https://code.visualstudio.com/docs/other/unity)に従い、旧 `com.unity.ide.vscode` は使用しない。UnityのExternal Script EditorにはVisual Studio Codeを指定する。

Unity Editor起動後、次のコマンドでソリューションと各アセンブリの `.csproj` を再生成できる。

```sh
uloop --project-path CityFlow execute-dynamic-code --code 'Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();'
```

VS Code選択時は `CityFlow/CityFlow.slnx`（新しいソリューション形式）と `CityFlow/CityFlow.*.csproj` が生成される。生成物と `CityFlow/.vscode/` はローカル環境用のためGit管理せず、手編集しない。

ワークスペースにはuloop経由のタスク `Unity: Compile`（既定のビルドタスク）と `Unity: Generate C# Projects` を用意している。デバッグでは `Attach to City Flow Unity Editor` を選び、起動済みのUnity Editorに接続する。

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

現在はIssue #1〜#10の検証都市、FlowNetwork集約、基本輸送、混雑・Source Overload、Overview操作・ホバー詳細、自動Ground経路とPreview、Node 360からの接続確定、Ground経路の手動編集、Lineの削除予約・取消・経路切替、Pause中の編集まで実装。Unity EditorのBootstrapシーンで確認できる。Wave・結果画面は後続の実装対象。空のモジュールは配置先だけを用意している。

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

## UIの編集

ゲーム内UIはUI Toolkitを使用する。`Assets/CityFlow/Runtime/Presentation/UI/ValidationHud.uxml` をUI Builderで開き、構造を編集する。見た目は同じフォルダーの `ValidationHud.uss`、画面スケーリングは `Assets/CityFlow/Settings/UI/ValidationPanelSettings.asset` に分けている。HUDとNodeラベルは `ValidationHud` が読み取り専用スナップショットから更新する。

BootstrapにUXML・PanelSettingsを割り当て済み。設定を再構成する場合は、uloopから `CityFlow.Editor.ValidationSceneSetup.Create()` を実行する。通常のPlayでは再構成不要。

## Overview操作

WASDまたは中ボタンドラッグでPan、ホイールでZoom、右ボタンドラッグでOrbit。Node/Lineを左クリックで選択し、Fでフォーカス、Homeで全景へ戻る。カーソルを合わせるとBuffer内訳・接続枠・停止原因・輸送性能を表示する。Input Systemの既定設定に従い、操作時はGame Viewにフォーカスを置く。

## Ground経路Preview

右下の **GROUND ROUTE PREVIEW** でFROM/TOを選び、**Generate route** を押す。建物を迂回する可視グラフ＋A*の候補を破線表示し、長さ・移動時間・推定Throughput・仮確定後の接続枠を確認できる。**Cancel** でPreviewを消す。初期ペアは `R1 → BLUE`。`S1 → BLUE` など既存の同方向接続は理由付きで確定不可と表示する。

この検証用パネル単独ではLineを追加しない。実際の接続は以下のNode 360フローを使う。制御点の手動編集は下記の操作で行う。

## Node 360で接続する

Sourceから新規配線を試す場合は **`Assets/CityFlow/Scenes/WiringLab.unity`** を開いてPlayする。同じ都市形状で初期Lineは0本、Sourceの暫定生成間隔は1 s。`S1 → BLUE` を確定するとFLOWが流れ始める。赤Sinkへの接続も追加できる。`Bootstrap` は引き続き固定5本の輸送検証用。

1. OverviewでNodeを選び、**Connect** または **C** を押す。WiringLabでは `S1 → BLUE`、Bootstrapでは `R1 → BLUE` が接続可能な例。Bootstrapの `S1` は初期配線でOUTが3/3のため、接続先を選ぶと枠不足や重複の理由を表示する。
2. 始点付近から、右ボタンドラッグまたは矢印キーで周囲を見る。All/Near/Mid/Farで候補を絞る。暫定閾値はステージ対角長の25%・50%で、検証都市では37.5 m・75 m。距離はGround面上の直線距離。
3. 候補マーカーをクリックすると自動Previewを生成する。**Tab** で候補に注目し、**Space** で選択も可能。画面外の方向表示・遮蔽中マーカー・接続不可候補も選べる。注目中の詳細に種類、Sink色、IN/OUTと失敗理由を表示する。
4. **Review in Overview / V** で経路全体を確認できる。再度VでNode 360へ戻り、始点・終点・Previewを保持する。
5. **Confirm Line / Enter** で経路と両端枠を再検証し、成立時にだけLineを追加する。**Cancel connection / Backspace** で枠を消費せず取消。どちらも開始前のOverview位置・角度・ズームへ戻る。

Escは後続のPause機能用に予約しており、接続取消には使用しない。Node 360でも輸送は進行する。

## Ground経路を手動編集する

Node 360で接続先を選び、**Edit Ground route / E** を押す。真上の見下ろし表示で **Shift+クリック** すると最寄り区間へ制御点を追加する。番号付きハンドルをドラッグして移動し、選択して **Delete** で削除する。A/Bの端点とGround高さは固定。

**Regenerate automatic route** で自動経路へ戻し、**Apply Line / Enter** で確定、**Cancel / Backspace** で配線全体を取り消す。建物を横切る区間は赤色と理由を表示し、適用不可になる。WASD/中ボタンドラッグとホイールで編集中もPan/Zoomできる。

## 運行中Lineの変更

OverviewでLineをクリックすると、**Edit selected Line / Reserve deletion / Cancel pending change** が表示される。編集適用または削除予約で新規流入を止め、既存FLOWが終点へ届くまで待つ。終点満杯時は **Buffer space** 待ちを表示する。完了前は取消できる。空のLineは即時完了する。接続枠は削除完了まで占有し、経路変更では維持する。

## Pause中に配線する

**Esc / Pause** で生成・輸送・Overload猶予・ゲーム経過時間をまとめて停止し、再度 **Esc / Resume** で再開する。停止中もカメラ、ホバー、配線、手動編集、削除予約・取消を操作できる。Escは編集を取り消さない。FLOWが残る予約処理は再開後に進む。
