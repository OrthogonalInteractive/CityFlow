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
5. `Assets/CityFlow/Scenes/WiringLab.unity` を開き、`uloop --project-path CityFlow control-play-mode --action Play` で起動する。簡易都市・5 Node・0 Lineから開始し、Sourceの準備時間中やPause中に配線する。左上HUDはDELIVEREDとTIMEのみ。NodeとLineの情報はホバー中だけ確認できる。設定は `Assets/CityFlow/Settings/Gameplay/` で調整する。固定5本の輸送検証にはBootstrapを使う。

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

現在はIssue #1〜#11の検証都市、FlowNetwork集約、基本輸送、混雑・Source Overload、Overview操作・ホバー詳細、自動Ground経路とPreview、Node 360からの接続確定、Ground経路の手動編集、Lineの削除予約・取消・経路切替、Pause中の編集、Wave・Node追加・結果と再試行まで実装。ゲーム性はUnity EditorのWiringLabシーンで配線0から確認できる。Bootstrapは固定配線の輸送検証用。比較プレイテストと難度の最終調整はIssue #12で行う。空のモジュールは配置先だけを用意している。

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

PLATEAU SDKはまだ導入していない。対象都市・詳細度・性能目標は未決定。v0.1初回のNode追加は制作者のWaveスケジュールを採用する。

## UIの編集

ゲーム内UIはUI Toolkitを使用する。`Assets/CityFlow/Runtime/Presentation/UI/ValidationHud.uxml` をUI Builderで開き、構造を編集する。見た目は同じフォルダーの `ValidationHud.uss`、画面スケーリングは `Assets/CityFlow/Settings/UI/ValidationPanelSettings.asset` に分けている。HUDとNodeラベルは `ValidationHud` が読み取り専用スナップショットから更新する。

BootstrapにUXML・PanelSettingsを割り当て済み。設定を再構成する場合は、uloopから `CityFlow.Editor.ValidationSceneSetup.Create()` を実行する。通常のPlayでは再構成不要。

## Overview操作

WASDまたは中ボタンドラッグでPan、ホイールでZoom、右ボタンドラッグでOrbit。Source／Relayの左クリックで360モードへ入り、Lineの左クリックで選択する。Sinkは始点にならず、クリック時に理由を短く表示する。Fでフォーカス、Homeで全景へ戻る。カーソルを合わせるとBuffer内訳・接続枠・停止原因・輸送性能を表示する。Input Systemの既定設定に従い、操作時はGame Viewにフォーカスを置く。

## Ground経路Preview

独立した検証用パネルは廃止。Node 360で接続先へホバーすると、建物を迂回する可視グラフ＋A*の候補を破線表示する。長さ・移動時間・推定Throughput・接続枠・無効理由は配線操作欄で確認し、Eで手動編集できる。PreviewだけではLine・接続枠を消費しない。

## Node 360で接続する

Sourceから新規配線を試す場合は **`Assets/CityFlow/Scenes/WiringLab.unity`** を開いてPlayする。同じ都市形状で初期Lineは0本、Sourceの暫定準備時間は15 s、生成間隔は3 s（準備後に最初の間隔を経て生成）。`S1 → BLUE` を確定するとFLOWが流れ始める。赤Sinkへの接続も追加できる。`Bootstrap` は引き続き固定5本の輸送検証用。

1. OverviewでNodeをクリックすると、そのNodeの360モードへ入る。**C**でも選択Nodeから開始できる。Connectボタンは不要。
2. 右ボタンドラッグまたは矢印キーで周囲を見る。All/Near/Mid/Farで距離を絞る。
3. Node・候補マーカー・右の **CONNECTION TARGETS** にホバー、または **Tab** で候補に注目すると、自動Previewが表示される。無効な候補も理由を確認できる。
4. Node・マーカー・一覧を**クリックすると接続確定してOverviewへ戻る**。無効な経路・接続枠不足では確定しない。確定前に **E** で手動編集、**V** でOverviewの経路確認も可能。
5. **Enter**でも確定、**Backspace**で取消できる。取消は接続枠を消費しない。

EscはPause／再開に使い、接続取消には使用しない。Node 360でも輸送は進行する。

## Ground経路を手動編集する

Node 360で接続先を選び、**Edit Ground route / E** を押す。真上の見下ろし表示で **Shift+クリック** すると最寄り区間へ制御点を追加する。番号付きハンドルをドラッグして移動し、選択して **Delete** で削除する。A/Bの端点とGround高さは固定。

**Regenerate automatic route** で自動経路へ戻し、**Apply Line / Enter** で確定、**Cancel / Backspace** で配線全体を取り消す。建物を横切る区間は赤色と理由を表示し、適用不可になる。WASD/中ボタンドラッグとホイールで編集中もPan/Zoomできる。

## 運行中Lineの変更

Overviewで **Shift＋Lineクリック** すると直接編集へ入る。通常のLineクリックでは、**Edit selected Line / Reserve deletion / Cancel pending change** が表示される。編集適用または削除予約で新規流入を止め、既存FLOWが終点へ届くまで待つ。終点満杯時は **Buffer space** 待ちを表示する。完了前は取消できる。空のLineは即時完了する。接続枠は削除完了まで占有し、経路変更では維持する。

## Pause中に配線する

**Esc / Pause** で生成・輸送・Overload猶予・ゲーム経過時間をまとめて停止し、再度 **Esc / Resume** で再開する。停止中もカメラ、ホバー、配線、手動編集、削除予約・取消を操作できる。Escは編集を取り消さない。FLOWが残る予約処理は再開後に進む。

## 配線0からゲーム性を確認する

1. `Assets/CityFlow/Scenes/WiringLab.unity` を開いてPlayする。今までと同じ簡易都市で、初期Lineは0本。
2. S1からRED・BLUEへ接続する。最初はSourceに15秒の準備があり、**Esc**で時間を止めて配線してもよい。
3. 60 / 120 / 180秒にWaveが進み、緑 / 黄 / 紫のSinkやSource・Relayが加わる。追加通知をクリックすると出現Nodeへ寄れる。追加Sourceは20秒準備し、既存ネットワークとFLOWは維持される。
4. 接続枠・距離・固定容量・局所Routingを見ながら、配線追加・経路編集・削除予約で混雑を解消する。Wave進行に伴い生成間隔は基本値の0.9 / 0.75 / 0.6倍になる。
5. SourceのOverloadが猶予を超えると結果を表示する。**Retry / 0 initial Lines** で同じ都市を配線0から再試行する。

Waveの時刻・追加Node・生成間隔と猶予は `WiringStage` で調整可能。現時点の値はプレイ確認用の暫定値であり、構成比較・難度調整・v0.1完了判定はIssue #12に残る。

Sourceでは生成時のリングと、脇に並ぶ色付きの待機FLOWを確認できる。生成累計・直近色・Buffer数／上限・空き数・準備時間・敗北までの残り秒数はNodeへホバーして確認する。カーソルを外すと詳細は消え、選択で固定されない。Node 360では始点表示や候補にもホバーできる。Pause中もホバー可能で、生成演出と敗北カウントダウンは停止する。敗北時はGAME OVER画面のRetryから配線0で再開できる。

FLOWの移動速度は視認性確認用の暫定 **8 m/s**（従来20 m/s）。表示だけでなく実際の移動速度を下げているため、Lineの回転と容量回復も遅くなる。最終的な速度・生成量・猶予の組み合わせは [Issue #13](https://github.com/OrthogonalInteractive/CityFlow/issues/13) の比較プレイで決める。


作成直後の空Lineは6秒以内なら **Undo / Ctrl+Z（MacはCmd+Zも可）** で取り消せる。FLOWが入ったLineや、編集・削除予約の巻き戻しには使わない。

## FLOWの送り先とBufferの基本設定

同色Sinkへの直結を優先し、そのLineが満杯なら待機する。直結がなければ空いているRelay行きLineだけから選ぶ。例としてS1の接続がREDとR1なら、RedはREDへ、BlueはR1へ送る。Relay行きも満杯ならS1で待つ。異色Sinkへは流さない。

Source／RelayはBufferに入っているFLOWを古い順に出力する。出られない色は残して、出られる後続FLOWも評価する。満杯でも出力は継続し、対応色Sinkを後からつなげば次のtickから排出・受け取り再開できる（Pause中は再開後）。

NodeラベルのBufferゲージは **1枠＝1 FLOW**。左から古い順に目的色を表示し、暗い枠は空き、ラベルの数値は実数／容量を示す。満杯でも色を保ち、外枠だけを警告色にする。Sourceの容量超過時は全FLOWを表示するよう枠が縮み、数値で超過を確認できる。

BufferはSource（S1/S2/S3）**10**、Relay **5**。SinkにはBuffer・中継・Outgoing Lineがなく、同色FLOWを到着時に消化する。Relay満杯の6個目はLine上で待ち、Relayの混雑だけでは敗北しない。Sourceは現在、**10個以上が5秒続くとGame Over**。上限到達と同時の敗北ではなく、10未満へ回復すると猶予がリセットされる。


LineのIn-Flight上限は **3**。待機間隔はLineの実経路長の1/3で、満杯時は始点から1/3・2/3・終点に分散する。移動中も間隔を確保し、停止・再開で位置を付け替えない。受け取り待ちのLineはオレンジに変わり、解消後は通常色に戻る。削除予約・経路切替の色は保持し、同時に詰まった場合は矢印をオレンジにする。

Sourceの生成間隔は以前の約3倍へ減速。WiringLabの基本値は **S1/S3: 3秒、S2: 3.9秒**、Wave倍率は従来どおり。Bootstrapの負荷検証用Sourceも0.25→0.75秒へ変更した。最終的な難度はIssue #13で比較する。
