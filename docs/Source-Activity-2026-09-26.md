# Sourceの最新FLOW一覧（2026-09-26）

左上のDELIVERED・TIME・Waveの下に、Source IDと最後に生成したFLOWを表示する。色バッジ・頭文字・色名を併記し、未生成は「—」。Bufferの先頭や最後に転送した色ではなく、NetworkSnapshotの最終生成色を使う。配送後・Pause中も保持し、Waveで追加されたSourceを自動で一覧へ加える。

SourceActivityRow.uxmlとValidationHud.ussで行の構造・見た目を定義し、ValidationHudが状態を反映する。読み取り専用でワールド入力を遮らず、ゲーム状態を変更しない。Node 360／経路編集中は従来の基本HUDとともに隠す。Sourceの詳細・接続一覧は引き続きホバーで確認する。

## 検証

- Red：追加したPlayMode 2件が、Source行と一覧要素の欠如で失敗することを確認。
- Green：関連PlayMode **26/26成功**。ValidationHud、HudPresentation、HoverHud、NodeHighlight、PauseInput、WaveSessionを実行。
- 新規テストで未生成、最新色への更新、Buffer排出後・Pause中の保持、Snapshotと時計の不変、Wave追加、Sourceごとの独立した値、UIDocument再生成時の復元、入力非遮蔽を確認。
- 東京駅を正規のWave進行と参照配線で3分30秒まで進め、Wave 3の3 Sourceを確認。65配送、Buffer内FLOW 0の状態でもS1=Green、S2=Yellow、S3=Greenを表示し、Snapshotと一致。
- 1600×900・1036×757で一覧の配置と文字・色バッジを確認。
- コンパイルError／Warning **0**、画面確認後のConsole Error／Warning **0**。ExpansionLabのPlayModeテストは実行していない。
- 確認後はPlay Modeを停止し、検証用配線・進行を破棄。シーン・Stageの設定は変更せず、Editor設定・Gameビューのサイズを検証前に戻した。

![東京駅Wave 3のSource一覧](screenshots/tokyo-source-list.png)
