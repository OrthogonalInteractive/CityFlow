# Editor一時停止の解除でゲームが進む問題（2026-09-27）

Unity EditorのPause解除直後に、停止していた実時間を一括でシミュレーションへ渡していた。原因は`SimulationDriver.Update`の`Time.unscaledDeltaTime`。Unity 6000.4.7f1の実セッションでは、再開した1フレームの値が`deltaTime = 0.010845秒`に対して`unscaledDeltaTime = 247.03656秒`だった。Stepでの1フレーム送りでは両方が約0.02秒になり、通常のPause解除で再現した。

`SimulationDriver`から渡す値を`Time.deltaTime`へ変更した。FLOW生成・輸送・Wave・Overloadを同じゲーム時計で進め、停止中の実時間を加算しない。Domain／Applicationの明示的なTick、固定0.05秒刻み、ゲーム内Pauseの状態、UI・カメラ用の時間源は変更していない。

## 再現と確認

- 計測時は再開フレームだけSimulationDriverを無効化し、元のゲーム状態を進めずに時計を比較した。
- 新しいPlayModeテストはUnity Editorを実際に2秒停止し、Editorの更新コールバックで再開する。Unityの再開フレームを検証する統合テストなので、この箇所だけ実時間の停止を使う。
- Red：修正前は通常ゲーム時間から許容される約0.199秒に対し、ゲーム時計が1.45秒進んで失敗。SourceのOverload猶予に達するまで一括更新していた。
- Green：関連PlayMode **38/38成功**、EditModeのPause／Wave **12/12成功**。停止中のSnapshotと時計の保持、再開後の通常更新、生成数・In-Flight位置・Wave・Overload、ゲーム内Pause状態の維持を確認した。
- PlayModeはSimulationDriver、FlowSimulation、PauseInput、WaveSession、NodeArrival、SourceStatus、OverviewZoom、LineReveal、TokyoStationWiringLab、ValidationHudを実行。ExpansionLabのPlayModeテストは作成・実行していない。
- 東京駅シーンで、実際のSimulationDriverを有効にしたまま約61秒のEditor Pauseから再開。`deltaTime = 0.006614秒`、`unscaledDeltaTime = 60.976284秒`だったが、固定tickに満たないためゲーム時計は24.85秒を保持し、生成数は2のまま、Game Overなし。再開した最初のフレームの描画時点で再びEditor Pauseに戻して確認した。
- コンパイルError／Warning **0**、テスト後Console Error **0**。既知のPLATEAU橋梁メッシュの大きな三角形に関するWarningあり。テスト用Editor設定は元に戻し、検証で再起動した東京駅シーンをEditor一時停止状態で残した。ゲーム内Pauseは解除状態、SimulationDriverは有効で、そのままEditorのPauseを解除できる。
