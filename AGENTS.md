# City Flow 開発ガイド

## 仕様と作業範囲

- ゲーム仕様の基準は `City-Flow-Specification.md`。作業前に対象バージョンと該当節を読む。
- 現在の実装対象は **v0.1とv0.2 Δ1（高さ方向）**。v0.2の残りとv0.3は差分仕様であり、先行実装しない。ただし、2026-09-26の指示によりPLATEAU SDKの導入・互換性検証と、東京駅周辺の独立した都市データ確認シーンを先行する。ゲームへの統合は別途扱う。
- 「補完案」「暫定値」「未決定」を確定事項と区別する。変更・採否は文書とテストに残す。
- 2026-09-26の指示により **#19のWave領域拡張（v0.2 Δ1.1）** と広域評価用`ExpansionLab`を対象に含める。このシーンのPlayModeテストは作成・実行しない。領域拡張のルール・配置検証はEditMode、見た目・ゲーム性はuloopでのプレイ確認と評価記録を使う。
- アーキテクチャは `docs/Architecture.md`、テスト方針は `docs/Testing.md`、起動方法は `README.md` を参照する。
- Unityプロジェクトのルートはリポジトリ直下の `CityFlow/`。`Assets`、`Packages`、`ProjectSettings` はその配下に置き、さらに入れ子のUnityプロジェクトを作らない。

## 技術スタック

- Unity **6.4 / 6000.4.7f1**、URP、Input System、Cinemachine 3、VFX Graph、Shader Graph。
- ゲーム内UIは **UI Toolkit**（`UIDocument` / UXML / USS）を使う。開発用HUD・Nodeラベルも対象とし、uGUIやIMGUI（`OnGUI`）で新規実装しない。
- Node名・種別の常設ラベルは表示しない。Source／Relayは待機FLOWがある間だけBufferゲージを表示し、1枠1 FLOWで待機順の色と頭文字（R/B/Y/G/P）を示す。空き枠と実数／容量を残し、満杯警告でFLOW色を上書きしない。SinkにはBufferゲージを表示しない。
- OverviewでNodeをホバー／Fフォーカスした時は、対象と直接つながる入出力Line・相手Nodeを明るく残す。それ以外の建物・地面・Node・Line・FLOW・ゲージを暗くする。接続を再帰的にたどらず、状態や当たり判定を変更しない。Node 360と経路編集中は通常の候補表示・半透明表示を使う。
- 詳細情報はNode/Lineのホバー中だけ表示する。常設のNETWORK INSPECTOR・Source詳細・Node接続表・DIRECTED LINES表・独立Ground Previewパネルを復活させない。基本HUDはDELIVERED・TIME・Waveの1行、危険時はSourceのワールド警告と状況ヒントを使う。
- UIの構造・見た目はUXML / USS、状態の反映・画面座標への変換はPresentationのC#へ分ける。Viewport比率・余白・安全距離はUSSに置き、座標変換と重なり回避は共通化する。UIにゲームルールや状態の正本を持たせない。表示専用の要素はワールドへの入力を遮らないようにする。
- 非同期処理は **UniTask**、通知・購読は **R3**、依存性注入は **VContainer**。
- Unity Editorの起動・コンパイル・テスト・Play Mode制御・ログ取得・シーン／アセット操作は、すべて **uloop CLI** を通して行う。Unity実行ファイルの直接起動、`-batchmode`、Editorの手動操作を自動化手段として使わない。
- リポジトリルートから `uloop --project-path CityFlow <command>` を基本形とする。利用可能なコマンドは `uloop --project-path CityFlow list` で確認し、ツール構成変更後は `uloop --project-path CityFlow sync` を実行する。
- Unity側の変更後は、まず `uloop --project-path CityFlow compile`、次に関連テストを `uloop --project-path CityFlow run-tests` で実行し、`uloop --project-path CityFlow get-logs` でConsoleを確認する。
- R3はNuGet側のコアとUPM側のUnity連携を両方導入する。`CityFlow/Assets/packages.config` と `CityFlow/Packages/manifest.json` を一緒に確認する。
- パッケージはバージョンまたはGitのタグ・コミットを固定する。UPMが生成する `CityFlow/Packages/packages-lock.json` も管理する。
- PLATEAU SDKは **v4.3.0** に固定する。実在都市ステージはv0.3で実装し、SDK固有型をDomain・Applicationへ公開しない。セットアップ範囲と検証結果は `docs/PLATEAU-Setup.md` を参照する。
- InputはInput Systemで扱い、旧 `UnityEngine.Input` を新規使用しない。
- 時間停止・再開、確定、編集、削除、Undoは画面内ボタンで操作する。Escは360・経路編集・選択のキャンセル専用とし、時間を変更しない。Enter／BackspaceやボタンのキーボードSubmitで処理を実行しない。カメラ操作とShift＋クリックは維持する。
- プレイヤー向けのLine操作名は「削除」（Delete）。FLOWを排出してから完了する不変条件は維持し、画面に「削除予約」（Reserve deletion）を表示しない。
- Node 360へ入る初期方向は直前のOverview画面上方向をGroundへ投影した方角とする。CONNECTION TARGETSでは固定の候補情報とPreviewを更新し、詳細ホバーパネルを重ねない。
- Node 360のミニカメラは接続元Node中心・北上固定の表示専用とする。候補変更で中心を移動せず、Overview・手動編集・Game Overでは描画を止め、シーン終了時にRenderTextureとカメラを解放する。

## DDDと依存方向

- ゲームの用語を型・API・テスト名に使う。FLOW Routingと幾何経路を作るLine Routingを混同しない。
- `Domain` は不変条件、集約、値オブジェクト、ドメインサービスを担当する。
- `Application` はユースケース、更新順序、トランザクション境界、外部機能のポートを担当する。
- `Infrastructure` は外部ポートの実装、Unity APIを使う空間問い合わせ、データ読み込みを担当する。
- `Presentation` は入力、カメラ、UI、描画を担当する。ゲームルールをViewやMonoBehaviourへ埋め込まない。
- `Composition` はVContainerの構成ルート。具象実装の選択とライフタイムの組み立てをここへ集める。
- 依存は `Application → Domain`、`Infrastructure → Application / Domain`、`Presentation → Application / Domain`、`Composition → 各層`。逆参照・循環参照を作らない。
- `.asmdef` で境界を表す。EditorコードとテストをPlayerのランタイムアセンブリから分離する。
- **Domainはpure C#である必要はない。** `UnityEngine.Vector3`、`Bounds`、`Mathf` などを使ってよい。独自の代替ベクトル型を作らない。
- テストの再現性のため、Domainから `Time`、`UnityEngine.Random`、シーン検索、Physicsのグローバル状態を直接取得しない。時間差分・乱数源・判定結果を入力として渡す。
- DomainへDIコンテナや描画ライブラリを持ち込まない。通常のC#オブジェクトはコンストラクター注入とし、Service Locatorを使わない。
- Repository、汎用基底クラス、イベントバス、インターフェースを形式のためだけに追加しない。必要な振る舞いと境界から導く。

## C#とnull許容参照型

- 自作のすべての `.cs` の先頭に **`#nullable enable`** を記載する。テスト・Editorコードも対象。
- 参照型は原則非null。欠如が正当な値だけ `T?` とし、境界で検証する。
- `null!`、`#nullable disable`、警告抑制で不整合を隠さない。Unityのシリアライズ等で必要な限定的例外は、検証方法と理由を英語コメントで明記する。
- `UnityEngine.Object` の破棄済み判定はUnityの `== null` / `!= null` を使う。`?.` や `??` は破棄済み判定の代用にならない。
- 自作コードのnullable警告を放置しない。外部パッケージ・生成コードは一括改変しない。
- UnityがサポートするC#・API範囲で記述する。Unity生成の `.csproj` を手編集しない。
- 名前空間は `CityFlow.<Layer>.<Module>`。1ファイル1主要型を基本とし、ファイル名を型名に合わせる。
- **コードコメント（XMLドキュメントコメントを含む）は英語。Gitコミットメッセージは日本語。** 識別子・テスト名も英語。設計書・作業報告は日本語。

## 非同期・通知・ライフタイム

- 1回の非同期処理はUniTask、状態変化・入力通知のストリームはR3を使う。同期で足りるドメイン操作を非同期化しない。
- 非同期APIには必要な `CancellationToken` を渡し、シーン・オブジェクト・セッションの寿命に連動させる。
- `async void` はUnity等が要求する境界以外では使わない。`.Forget()` は例外処理とキャンセルの所有者を明確にした境界に限定する。
- R3の購読は所有者が破棄する。変更可能なSubjectやReactivePropertyを外部へ無制限に公開しない。
- ゲームの時間とUI・カメラの時間を分離する。Pauseでシミュレーションを停止しても配線編集・情報確認を止めない。
- シミュレーションは明示的なtickで進める。UniTaskのDelayやR3のTimerを輸送ルールの時間源にしない。

## TDD

1. 対象の仕様節から、最小の振る舞い・境界条件をテストにする。
2. テストを実行し、意図した理由で失敗することを確認する（Red）。
3. テストを通す最小の実装を行う（Green）。
4. テストを維持しながら設計を整理する（Refactor）。

- バグ修正は再現テストから始める。仕様変更では該当テストを先に更新する。
- Domain / ApplicationはEditMode中心。シーン、入力、VContainerの寿命、PlayerLoop、描画連携は必要なPlayModeテストで確認する。
- 実時間待ち・フレーム数・グローバル乱数に依存するルールのテストを避け、時間差分と固定シード等を使う。
- publicな振る舞いと不変条件を検証する。実装の写し、常に成功するテスト、空のテストで件数を増やさない。
- 文書・空のフォルダー等の変更に無理にテストを作らない。構成変更はUnityでのインポート・コンパイルと関連する統合テストで確認する。
- 完了時は実行したチェックと結果を報告する。実行できなかったものを成功扱いしない。

## 守るべき不変条件

- `MaximumAltitude = 0` のv0.1ステージではNode / LineはGround上。高さ有効時はRelay直上の垂直区間と一定YのXZ経路のみを許可し、YとXZを同時に変える斜め配線を禁止する。RelayのMaximumRiseは配置位置からの上昇距離[m]で、始点・終点の双方とステージ上限を満たす。Source／Sinkは配置高度固定。経路途中で昇降せず、建物の3Dボリュームを避ける。全区間の衝突判定と描画・距離計測・移動の実経路を一致させる。
- Lineは有向で固定容量（基本3）。Lineが長くても容量を増やさない。待機間隔は実経路長 / 容量とし、停止時にFLOWの位置を付け替えたり後退させたりしない。
- 同色Sinkへの直結を距離より優先し、その直結がすべて満杯なら待機する。直結がなければ、途中の直結優先も織り込んだ同色Sinkまでの合計実経路長が最小となるRelayを選ぶ。満杯・到達不能なら待機し、等距離の空き候補だけを代替に使う。異色Sink・Source・長い経路へのランダム転送をしない。FLOW Routingの経路表は接続・予約・経路変更で失効させ、In-Flightの経路を変えない。
- SourceはOUTのみ、RelayはIN／OUT、SinkはINのみ。定義・配置は種別ごとの型で表現し、SourceのINとSinkのOUTを設定項目にしない。SourceへのLineは作成できない。
- Sinkは同色FLOWを即時消化する終点で、Buffer・中継・Outgoing Lineを持たない。Bufferの基本設定はSource 10、Relay 5。
- FLOWの受け渡し完了前にLineの容量を解放しない。停止中もIn-Flightを保持する。
- 削除予約・経路切替は新規流入を止め、排出完了を待つ。FLOWの消失・瞬間移動・接続枠の先行解放を起こさない。
- Relayの混雑だけではGame Overにしない。Sourceの継続Overloadが敗北につながる。
- Waveは同じシーン内で進行し、既存ネットワークとFLOWを維持する。
- v0.1初回のNode追加は制作者が設定するWaveスケジュールを使う。自動生成をMVPの必須要件にしない。

## Unityアセットと作業上の注意

- 自作アセットは `CityFlow/Assets/CityFlow` 配下。アセットと `.meta` を一緒に管理し、移動時はGUIDを維持する。
- シリアライズはForce Text、Version ControlはVisible Meta Files。
- `Library`、`Temp`、`Logs`、`UserSettings`、ビルド・テスト出力、復元されたNuGet DLLをコミットしない。
- ゲームの正本はDomainの状態。ScriptableObjectは調整用設定、VFX Graphは可視化を担当し、輸送状態を所有しない。
- 設定値は単位と仕様上の状態（暫定など）を明記する。距離の基準は1 Unity unit = 1 m。
- 関連するテストはuloop経由で実行し、不要なアセット再保存やパッケージ更新を差分に混ぜない。
