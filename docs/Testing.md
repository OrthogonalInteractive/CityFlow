# TDDと検証

## 実行

初回に `dotnet tool restore` と `dotnet nugetforunity restore CityFlow` をリポジトリルートで実行する。Unity Editorの起動、コンパイル、テスト、ログ取得はすべてuloop CLIを使う。

```sh
uloop launch CityFlow
uloop --project-path CityFlow compile
uloop --project-path CityFlow run-tests --test-mode EditMode
uloop --project-path CityFlow run-tests --test-mode PlayMode
uloop --project-path CityFlow get-logs
```

`run-tests` の応答に含まれる実行件数・成功・失敗を確認し、テスト未実行を成功扱いしない。Unity Consoleにコンパイルエラーや例外がないことを `get-logs` で確認する。
Unity CLI Loopのツールが更新された場合は `uloop --project-path CityFlow sync` を実行する。利用可能なコマンドは `uloop --project-path CityFlow list` で確認する。

## Red → Green → Refactor

1. 仕様の節と、今回扱う振る舞いを選ぶ。「補完案」はその旨を記録する。
2. publicな振る舞いを検証する最小のテストを書く。
3. 実行して意図した失敗を確認する。依存DLL不足など環境上の失敗とは区別する。
4. 最小限の実装で通し、重複と責務を整理する。
5. 影響範囲のテストを再実行する。

DomainはUnityEngine参照を許可したままEditModeでテストする。独自Vector3やUnityの代替スタブを作らない。
時間・乱数・空間判定の入力を制御し、実時間待ちや偶然の分岐に依存しない。

## 検証対象

仕様§18ごとの実装・テスト対応は [完了条件の追跡](V01-Acceptance.md)、過去のRed/Greenと画面記録は [検証履歴](history/Testing-v01.md) を参照。

| 順序 | 仕様 | 主に確認する振る舞い |
| --- | --- | --- |
| 1 | v0.1 §3–6 | 同色Sinkの即時消化、異色Sinkへの転送禁止、Relay入力停止と再開、接続枠の境界 |
| 2 | §7 | 同色直結優先、合計実経路長の最短Relay選択、到達不能／最短満杯時の待機、予約・取消・経路切替後の再評価、後続の別色が出発できること |
| 3 | §8–9 | FLOWの所属の一意性、受け取り完了まで容量を保持、追い越し防止、長さと容量の独立性 |
| 4 | §8.2・§11 | 削除予約と取消、詰まった終点での排出待ち、経路切替、接続枠の保持・解放、FLOW保存 |
| 5 | §9–11 | XZ経路長、Ground固定、制御点が有効でも区間が衝突するケース、Previewの非破壊性 |
| 6 | §5.2・§14.1・§16 | Sourceのみの敗北、猶予リセット、Pause中の更新停止と編集、Wave後の状態維持 |
| 7 | §12–15 | 入力→ユースケース→表示、カメラ切替時のPreview保持、ホバー、キャンセル、購読破棄 |
| 8 | v0.2 Δ1 | 屋上／空中Node、3D全区間衝突、上越し／横迂回、Y編集、3D長さと輸送・描画の一致、高さ付きWave |

Source自身の超過生成、同色直結満杯時の待機、削除・切替の相互排他など、補完案に依存するテストには仕様節とその前提を英語コメントで残す。

## テストの配置

- `Tests/EditMode`：Domain / Applicationの振る舞い、Unityアセット・設定の検証。機能を追加したら対象モジュールごとのフォルダーへ分ける。
- `Tests/PlayMode`：シーン起動、DIの寿命、PlayerLoop、Input SystemとPresentationの連携。
- 見た目・操作感：EditorでOverview / Node 360 / 停止中の編集・ホバーを確認。自動テストの成功で描画品質を保証したことにしない。

基盤テストはURP割当、起動シーン登録、Input System設定、起動時のVContainer構築、UniTask / R3のPlayerLoop連携と購読破棄を確認する。
設定・接続・輸送・削除予約・経路編集・Wave・Pause・入力・ホバーの回帰テストを維持する。新規不具合は再現テストを先に追加する。

## 完了時の報告

実行したモード・件数・結果と、実行できなかった検証を明記する。
nullable警告や自作コードのコンパイルエラーを残さない。外部パッケージの警告は自作コードと区別し、全体の警告抑制で隠さない。

## 現在の検証状況

2026-09-25の`codex/v02-height`を`main`（`8842dc8`）にrebaseした後の検証は、全EditMode **171/171**、全PlayMode **64/64**成功。ボタン操作・Escキャンセル・Node 360初期方向・候補ポップアップ抑制・ミニカメラと高さ編集を併合した。高さ編集後の合成クリック座標がUI Toolkitに残り候補ホバーが失敗するケースを再現し、テスト内でPointerMoveとMouse.currentの座標を同期して解消した。高さ入力＋ホバーの組合せ **7/7**、操作ヒントのボタン名調整後の関連HUD **4/4**でも確認。コンパイル・最終テスト後ConsoleのError/Warning **0**。HeightLabの屋上R1からBLUEへのPreview・ミニカメラも実画面で確認した。

2026-09-25のNode 360ミニカメラは、全PlayMode **61/61**成功。接続元中心・北上固定、候補／視線変更とPause、Overview・編集・Game Overでの描画停止、メインカメラへの非干渉、UIDocumentの再生成、シーン破棄時のCamera／RenderTexture解放を確認した。コンパイル・最終ConsoleともError/Warning **0**。1600×900と1036×757の実画面は [ミニカメラの対応報告](Node-Minimap-2026-09-25.md) を参照。

2026-09-25のNode 360候補ホバー・初期方向の修正は、全PlayMode **58/58**成功。一覧での詳細ポップアップ抑制とPreview・接続の維持、3種類のOverview角度×3 Nodeでの画面上方向の引き継ぎ、経路確認との往復・取消時のカメラ復元を確認した。コンパイル・画面確認後ConsoleともError/Warning **0**。[画面付きの対応報告](Node360-UX-2026-09-25.md) を参照。

2026-09-25のボタン操作・Escキャンセルは、全EditMode **158/158**、全PlayMode **57/57**成功。Pause／Resume、停止中の配線編集・確定、Escによる360・編集・Overview選択の解除、Enter／Backspace等での誤実行防止、Delete中のFLOW保持・取消を確認した。コンパイル・画面確認後ConsoleともError/Warning **0**。[画面付きの対応報告](Button-Controls-2026-09-25.md) を参照。

2026-09-23のv0.2 Δ1高さ対応は、全EditMode **171/171**、UI重なり修正後の全PlayMode **56/56**成功。専用worktree／Unity Editorで実行し、既存Groundステージも回帰確認した。コンパイルと撮影セッションのConsole Error/Warning **0**。テスト後にUnity Quick Searchの初期索引生成でEditor側例外が出ており、ゲームの例外と区別して[画面付き検証報告](Height-Routing-2026-09-23.md)へ記録した。

2026-09-22の直結優先＋最短距離配送は、最終EditMode **158/158**、PlayMode **53/53**成功。合計実経路長、途中の直結優先、到達不能・最短満杯時の待機、等距離の代替、予約・取消・経路切替、Wave追加後の経路表を確認した。コンパイル・テスト後Console Error/Warning **0**。[配送ルールの対応報告](Flow-Routing-2026-09-22.md) を参照。

2026-09-22の接続フォーカス・条件付きゲージは、最終全PlayMode **53/53**成功。直接接続だけの強調、孤立Node、接続削除への追従、F／Home／空白選択での保持・解除、Node 360への復帰、無関係なFLOW・Source演出・ゲージの減光を確認した。コンパイル・撮影後ConsoleともError/Warning **0**。[画面付きの対応報告](Connection-Focus-2026-09-22.md) を参照。

2026-09-22の障害物デザイン変更は、全PlayMode **49/49**、シェーダーの最終調整後の関連PlayMode **3/3**成功。コンパイル・撮影後ConsoleともError/Warning **0**。6棟の描画／Colliderと設定Boundsの一致、Node 360の透過／復帰、迂回配線を確認した。[画面付きの対応報告](Obstacle-Design-2026-09-22.md) を参照。

2026-09-22のNode方向の型分割・設定移行・表示変更は、全EditMode **146/146**、全PlayMode **48/48**成功。最終コンパイルError/Warning **0**。配置の種類変更・Undo・初期配置／Waveの移行と、SourceのOUT専用・SinkのIN専用表示を確認した。[画面付きの対応報告](Node-Directions-2026-09-22.md) を参照。

2026-09-22のUX改善は #14〜#17。全体検証はEditMode **130/130**、PlayMode **47/47**成功（Snapshot最適化まで）。その後のPauseヒント仕上げ・ホバー対象へのFフォーカス確認は、関連PlayMode **15/15**成功。最終コンパイルError/Warning **0**、撮影後Console Error/Warning **0**。

- Source 80%・猶予円弧・秒数・Pause・回復、混雑と予約の線種の併用、Sink始点禁止、空Line Undoを検証。
- Wave時計、カメラ移動後の通知回避、構造化ホバー、色の頭文字、自己候補除外、全マーカーの表示を検証。
- Snapshotの再利用、生成・接続・出発・輸送・予約・取消・Wave追加・Overload後の鮮度と、過去の読み取りの不変性を検証。変更のない1000回の読み取りでSnapshot実体は1000個→1個。FPSやGCバイト数の改善率は未測定。
- uloopによる実画面4枚を確認し、[UX対応報告](UX-Review-2026-09-22.md) に保存。制御したネットワークを撮影しており、人による難度比較の代用にはしない。
- 確認後はWiringLabを配線0・Pause・SimulationDriver有効へ戻した。現在はResumeボタンで開始できる。

途中のRed/Greenは [検証履歴](history/Testing-v01.md#2026-09-22-ux改善進行中) に残している。

#12 / #13の人による比較プレイ、難度パラメータの最終採用は未完了。Unity Editor内を対象とし、Playerビルド・PLATEAU・大規模都市の性能は検証対象外。高さ方向はv0.2 Δ1として専用HeightLabで検証する。
