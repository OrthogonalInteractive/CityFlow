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

## 最初の実装順とテスト候補

下表はMVP全体のテスト計画。実装・実行済みの範囲と結果は末尾のStep別記録を参照。

| 順序 | 仕様 | 主に確認する振る舞い |
| --- | --- | --- |
| 1 | v0.1 §3–6 | 同色Sinkの即時消化、異色中継、Relay入力停止と再開、接続枠の境界 |
| 2 | §7 | 同色直結優先、直結満杯時の待機、削除予約Lineの除外、後続の別色が出発できること |
| 3 | §8–9 | FLOWの所属の一意性、受け取り完了まで容量を保持、追い越し防止、長さと容量の独立性 |
| 4 | §8.2・§11 | 削除予約と取消、詰まった終点での排出待ち、経路切替、接続枠の保持・解放、FLOW保存 |
| 5 | §9–11 | XZ経路長、Ground固定、制御点が有効でも区間が衝突するケース、Previewの非破壊性 |
| 6 | §5.2・§14.1・§16 | Sourceのみの敗北、猶予リセット、Pause中の更新停止と編集、Wave後の状態維持 |
| 7 | §12–15 | 入力→ユースケース→表示、カメラ切替時のPreview保持、ホバー、キャンセル、購読破棄 |

Source自身の超過生成、異色Buffer満杯のSinkへの同色到着、同色直結満杯時の待機、削除・切替の相互排他など、補完案に依存するテストには仕様節とその前提を英語コメントで残す。

## テストの配置

- `Tests/EditMode`：Domain / Applicationの振る舞い、Unityアセット・設定の検証。機能を追加したら対象モジュールごとのフォルダーへ分ける。
- `Tests/PlayMode`：シーン起動、DIの寿命、PlayerLoop、Input SystemとPresentationの連携。
- 見た目・操作感：EditorでOverview / Node 360 / 停止中の編集・ホバーを確認。自動テストの成功で描画品質を保証したことにしない。

基盤テストはURP割当、起動シーン登録、Input System設定、起動時のVContainer構築、UniTask / R3のPlayerLoop連携と購読破棄を確認する。
設定・接続・基本輸送のテストはStep 01〜03で追加済み。未実装の削除・切替・進行・入力等は、引き続き失敗するテストから追加する。

## 完了時の報告

実行したモード・件数・結果と、実行できなかった検証を明記する。
nullable警告や自作コードのコンパイルエラーを残さない。外部パッケージの警告は自作コードと区別し、全体の警告抑制で隠さない。

## Step 01 検証結果

- 設定・配置のRed: 14件中13件失敗（無検証のため不正値を受理）、1件成功。
- Green: EditMode 19/19、PlayMode 3/3成功。コンパイルError/Warningとも0。
- Ground外、建物内・クリアランス内、異なる高さ、非有限値、不正容量、Sink色不足、Sourceの生成先不足を検証。
- アセットから読み込んだ定義の独立性、BootstrapのVContainer、初期Node 5個・2色Sink・URP描画を確認。
- EditorのGame Viewスクリーンショット: `docs/screenshots/issue-1-city.png`。

## Step 02 検証結果

- 接続・集約のRed: 11件中10件失敗、1件成功。個別失敗理由と接続成功・生成未実装を確認。
- Green: EditMode 30/30、PlayMode 3/3成功。コンパイルError/Warningとも0、Console Error 0。
- 両端枠の同時確保、自己接続・重複・OUT/IN不足、逆方向、全区間障害物判定、読み取り専用スナップショット、FLOW IDとBuffer所属を検証。
- PlayModeで5本の固定配線、IN/OUT合計、生成されない初期状態を確認。
- EditorのGame Viewスクリーンショット: `docs/screenshots/issue-2-network.png`。


## Step 03 検証結果

- Routing・輸送のRed: 21件中20件失敗、1件成功。無処理の生成・移動・出発を検出。
- 混雑がない同時出発への不要な減速を追加テストで再現（1件失敗）し、停止列の間隔を混雑時だけ適用して修正。
- 最終Green: EditMode **52/52**、PlayMode **4/4**成功。コンパイルError/Warning **0**、Console Error **0**。
- 直結優先、複数同色直結、満杯直結待機、後続別色、空きLine間の乱数分岐、異色Sink中継、同色即時消化、固定容量、長距離の容量回復遅延を検証。
- 受け取り完了までのLine所属、FIFO、満杯異色Bufferでの同色受け取り、複数Incomingの容量競合、FLOW保存と一意性を検証。
- 0.05 s固定tickの生成→移動・受け渡し→出発を検証。30/60/144 fpsへ10秒を分割した場合と一括入力で、生成数・成功数・所属ID・移動距離が一致。
- PlayModeでは明示的tickによる生成・移動・消化、表示粒子の位置と消滅、設定アセット不変性を検証。元のEditor高速Play設定（Domain/Scene Reload無効）へ戻した後にも再起動を確認。
- スクリーンショット `docs/screenshots/issue-3-transport.png` はseed=1337、20秒時点で表示を固定して撮影。生成80、消化50、待機15、In-Flight15。長距離青Lineは10/10、短距離赤Lineは5/10。
- 対象はUnity Editor内。Playerビルドは今回の検証対象外。

## Step 03後：UI Toolkit移行の検証結果

- Red: UIDocument未導入の状態でPlayModeのHUDテスト3件が失敗。
- Green: EditMode **52/52**、PlayMode **7/7**成功。コンパイルError/Warning **0**、実画面確認後のConsoleログ **0**。
- HUDの処理成功数・Buffer・In-Flight・Node/Line一覧が実際のスナップショットへ追従することを確認。
- 表示専用要素が入力を遮らないこと、Nodeラベルのカメラ追従・投影座標・背後での非表示、UIDocument再有効化時の値の復元と行の重複防止をPlayModeで確認。
- 既存の都市起動・生成・移動・消化・設定不変性のテストも成功。元のEditor設定へ戻してGame Viewを確認。
- `docs/screenshots/ui-toolkit-hud.png` は移行後の20秒時点。生成80、消化50、待機15、In-Flight15で、移行前と一致。

## Step 04 検証結果

- Red: 新規8件がOverload判定・停止状態未実装により失敗。
- Green: EditMode **60/60**、PlayMode **9/9**成功。コンパイルError/Warning **0**。
- Sourceの満杯境界、5秒の連続猶予、回復時リセット、超過生成保持、Relayだけでは敗北しないこと、停止列のID・位置・容量保持と排出再開、不正時間入力、敗北後の状態保持を検証。
- PlayModeでSource警告→Game Over表示と停止を確認。輸送速度・Routingだけを検証する既存テストは猶予を1000秒へ設定し、敗北の検証とは分離。
- Play画面でRelay 50、Line停止10、Source 56（猶予残3.5秒）を再現。排出先追加で停止10件が受け渡され、FLOW総数の保存を確認。動的Line追加時のHUD例外も再現テスト（Red 1件）で修正。
- スクリーンショット: `docs/screenshots/issue-4-congestion.png`。

## Step 05 検証結果

- Red: Overview未構成のためPlayMode 3件が失敗。
- 表示値のEditModeテストでBuffer内訳・接続枠・実経路長・時間・容量使用率・推定Throughputを検証。
- PlayModeでPan/Zoom/Orbit、フォーカス、全景復帰、Node/Lineホバー・選択、Input Action経由のF操作、R3の完了・破棄、操作前後の輸送状態保持を検証。
- 仮想キーボード入力はEditorのGame Viewフォーカスに依存しない配送設定をテスト中だけ使い、finallyで元へ戻す。
- Green: EditMode **62/62**、PlayMode **12/12**成功。ゲージの見た目調整後もHUD関連4/4成功。コンパイルError/Warning 0、実画面確認後Console Error 0。
- uloop入力シミュレーションでもFによるフォーカスとHome復帰を確認。選択Lineの強調・詳細・Bufferゲージの画面: `docs/screenshots/issue-5-overview.png`。

## Step 06 検証結果

- 経路・PreviewのRed: 13件中11件失敗、2件成功。UI未構成のRed: PlayMode 2件失敗。
- Green: EditMode **79/79**、PlayMode **14/14**成功。コンパイルError/Warning **0**。
- 直線、左右迂回、再現性、クリアランスによる狭路可否、区間貫通、Ground高さ、領域外、不正制御点、未登録Node、I/O不足、重複接続を検証。
- Previewの非破壊性、取消、探索失敗時の端点保持、編集APIの全区間検証・端点固定、生成経路を接続したときの距離・実移動との一致を確認。
- PlayModeでUI Toolkitの生成/取消操作、破線描画、メトリクス、重複・衝突理由表示と既存HUD/Overviewを検証。
- 最終スタイル調整後のUI関連PlayMode **6/6**成功。uloopのマウス入力でもGenerateを確認。R1→BLUEの迂回経路は76.48 m、移動時間3.82 s、推定2.62 FLOW/s。生成後も確定Lineは5本で接続枠を消費しない。
- Game Viewで有効な半透明破線・矢印と、建物を横切る無効区間の赤表示・理由を確認。スクリーンショット: `docs/screenshots/issue-6-ground-preview.png`、`docs/screenshots/issue-6-invalid-route.png`。最終Consoleログ **0**。Unity Editorで検証し、Playerビルドは対象外。

## Step 07 検証結果

- Red: 接続セッション12件中11件失敗、1件成功。Node 360未構成のPlayMode 4件失敗を確認してから実装。
- カメラ確認中に変更した選択の復元、始点メッシュによる視界遮蔽、生成済み候補の状態表示も各1件の回帰テストで再現して修正。
- Green: 全EditMode **91/91**、全PlayMode **20/20**成功。候補の最終表示修正後にも全PlayMode **20/20**成功。コンパイルError/Warning **0**、実画面確認後のConsoleログ **0**。
- 未選択→始点→終点→Preview→確定/取消、未登録Node、不正距離閾値、Near/Mid/Far境界、Ground距離、Sink色・I/O、自己接続・重複・枠不足を検証。
- Preview後に別接続でOUT/INを埋めた場合と、空間判定が変わった場合の確定直前再検証を確認。失敗時はPreviewを保持し、成功時だけ1本のLineと両端枠を確保。
- PlayModeで候補ボタン、遮蔽・画面外表示、距離フィルター、注目情報、Input SystemのC/矢印/Enter/Backspace、カメラ確認切替、位置・回転・ズーム・内部pivot・選択の復元、Controller無効化時の取消と再有効化を確認。
- `WiringLab` でSource→Sinkの接続からFLOW出発・到着・描画追従まで検証。固定5本のBootstrapでは同色直結満杯時にRelayへ迂回しないため、初期Lineなしのシーンで輸送開始を確認した。
- 実入力でもS1をクリック→C（Connectボタンのクリックも確認）→TabでBLUEへ注目→SpaceでPreview→VでOverview確認→Enterで確定。長さ100.98 m、移動時間5.05 s、推定1.98 FLOW/s。明示的tickを8秒進め、Line 1本、In-Flight 5、同色到着1、Overview復帰を確認。
- UI Toolkitの実ポインター検証ではUnity Editor本体に加えGame Viewへフォーカスする。撮影時はSimulationDriverの自動更新を止めて状態を固定し、手動tick以外の時間進行を除いた。
- スクリーンショット: `docs/screenshots/issue-7-node360.png`、`docs/screenshots/issue-7-review.png`、`docs/screenshots/issue-7-connected.png`。対象はUnity Editor内で、Playerビルドは検証対象外。

## Step 08 検証記録

- 手動制御点操作の3テストが未実装により失敗するRedを確認。実装後はEditMode **94/94**、PlayMode **22/22**成功。
- Ground投影、端点固定、点の追加・移動・削除、距離/時間/Throughput、無効適用拒否、自動再生成、非破壊取消を検証。
- UIの編集開始→真上カメラ→無効経路→再生成→追加→適用と、取消時のOverview復帰をPlayModeで検証。
- uloopの実Input System操作でSpace→E→Shiftクリック→Enterを通過。S1→BLUEの5点・101.934 mのLineを確定し、編集終了を確認。Errorログ0件。
- 画面: `docs/screenshots/issue-8-manual-route.png`。

## Step 09 検証記録

- 予約・排出・取消・経路切替の6ケースが未実装により失敗するRedを確認。競合予約の再検証を加え、EditMode **101/101**、PlayMode **24/24**成功。
- 削除完了前の枠保持、終点満杯での無期限待機、取消でのID/距離/経路保存、排出後の枠解放、新経路への切替、削除後IDの非再利用を検証。各シナリオでFLOW総数と所属一意性を確認。
- UIから予約・取消・編集適用し、状態とBuffer空き待ち表示、削除/切替後の描画・距離表示更新を検証。コンパイルエラー/警告0、Console Error0。
- 画面: `docs/screenshots/issue-9-delete-pending.png`。

## Step 10 検証記録

- Pause未実装で3テストが失敗するRedを確認。実装後はEditMode **104/104**、PlayMode **25/25**成功。
- Pause中の生成数・経過時間・Overload・FLOW位置・旧経路・tick端数の保存、再開後の残り距離からの継続、空Lineの即時削除と占有Lineの待機を確認。
- Input SystemのEscを手動編集中に送り、Previewを保持したまま停止、Wによるカメラ移動、停止中の配線適用、Esc再開をPlayModeで検証。シーン読込後の実行時間は初期値を仮定せず差分で評価。
- 実Esc入力の画面: `docs/screenshots/issue-10-paused-edit.png`。コンパイルエラー/警告0、Console Error0。

## Step 11 検証記録

- Wave/追加/新色/準備時間/結果の5テストが未実装により失敗するRedを確認。最終EditMode **112/112**、PlayMode **28/28**成功。コンパイルエラー/警告0、Consoleログ0。
- 初期Sourceの準備15 s、追加Sourceの準備20 s、Sink追加後の色候補更新、Waveをまたぐ予約・Buffer・FLOW ID・位置の維持、Pause中のWave/準備時間停止、倍率変更時の生成位相維持を確認。
- 0 LineからS1→RED/BLUE、Wave 2でS1/S2→GREEN等、Wave 3で各Sink→YELLOW、Wave 4でYELLOW→PURPLEとS3接続へ拡張。4 Wave・11 Node・5色・13 Lineで240 sまで生存し、100件超の配送とFLOW総数保存を確認。難度比較・採用値の最終判定はStep 12の対象。
- 未接続の追加Sourceを放置した画面確認ではWave 3・140.4 s・処理107・原因S2でGame Over。HUDと結果が一致。Retryボタンで新スコープ・Wave 1・0 Line・結果なしに戻ることを自動/画面操作の両方で確認。
- 追加Nodeの選択・Node 360・配線、出現通知・画面外方向を検証。画面内Nodeをパネル回避位置によって画面外と誤表示する不整合を再現テストでRedにし、画面外判定とラベル配置の分離後にGreenを確認。
- 画面: `docs/screenshots/issue-11-zero-lines.png`、`issue-11-wave.png`、`issue-11-result.png`。

## Node 360の候補表示修正

- S1のNode 360で、GREENの候補マーカーがREDと重なると非表示になり、マウスでは候補の存在を把握できない問題を修正。全候補を選べる一覧がないことをPlayModeの再現テスト1件でRed確認。
- 修正後はPlayMode **29/29**成功。Wave進行中の候補追加、GREENの表示領域とポインター到達、Near/Midフィルター、カメラ回転後の選択、確定前の非破壊性、S1→GREEN確定を検証。
- Game View上の一覧をInput Systemのマウス入力でクリックし、S1→GREENの54.9 mの有効Preview生成を確認。コンパイルエラー/警告0、Consoleログ0。Domain/Applicationの変更はなく、今回EditModeは再実行していない。
- 画面: `docs/screenshots/node-360-candidate-list.png`。確認用のWave 2を再作成し、ゲームPause・GREENの確定前Previewで停止。

## v0.1操作改善

- Nodeクリック開始・注目時Previewの新仕様でPlayMode 4件のRedを確認。
- PlayMode全体で29件成功、マウス入力テスト1件の失敗を切り分け。複数Mouseがあると別デバイスのPointer座標を読む問題を修正し、実クリック→360、Shift＋Lineクリック→編集の1件も個別再実行で成功。
- クリック発生元デバイスの位置を使用する。編集開始クリックで制御点が増えないこと、注目時にLineを作らず、候補クリックで確定して元のOverviewへ戻ることを確認。

## v0.1 Node 360の視認性改善

- 建物・屋根の半透明表示がない状態で再現テストのRedを確認。
- 接続・Wave関連PlayMode **12/12成功**。情報パネルと3D描画領域の非重複、注目ラベルがNode本体を覆わないこと、半透明時の深度書込停止、不透明マテリアルとCamera.rectの復元を確認。
- 材料の色を繰り返し書き換えることによる丸め変化を避け、元マテリアルを保存して透明版へ差し替える。
- 候補一覧のスクロール後にもGREENへポインターが到達し、クリックで確定できることを確認。

## v0.1 Source可視化と敗北の事前警告

- Sourceの常設Buffer表示と360中の敗北警告がないことをPlayMode 2件のRedで確認。実装後は2/2成功。
- 初回生成・待機粒子・生成リング、Pause中の表示と演出の停止、360中のモニターと都市領域の非重複を確認。50/50のSourceで残り4秒を表示し、Pauseで猶予を止め、再開後のGAME OVER表示とOverview復帰を確認。
- FlowNetworkのEditMode 12/12成功。生成と同じtickに出発してもSourceの生成累計・直近色が観測でき、過去のスナップショットは変化しない。コンパイルエラー/警告0。
