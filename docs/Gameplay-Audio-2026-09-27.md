# ゲームSE（2026-09-27）

試聴後の「じゃあ入れて」の指示により、確認済みの3音をゲームへ組み込んだ。外部ライブラリの音源ではなく、この作業で波形合成したオリジナルのPCM WAVを使う。試聴用の連続再生ファイルではなく単発の原音と同一のバイト列であることをSHA-256で確認した。

| イベント | アセット | 形式／長さ |
| --- | --- | --- |
| 初期Wave・Wave進行 | `WaveAdvance.wav` | 48 kHz / 16 bit / stereo / 1.45秒 |
| SourceからLineへ実際に出発 | `SourceDeparture.wav` | 48 kHz / 16 bit / mono / 0.18秒 |
| 同色Sinkで消化 | `SinkArrival.wav` | 48 kHz / 16 bit / mono / 0.40秒 |

Sourceの生成待ち／Buffer生成だけでは鳴らず、Relayの中継でも鳴らない。初回Waveは新しいセッション／Retryで鳴る。音源は `Assets/CityFlow/Audio`、設定は `Assets/CityFlow/Settings/Audio/GameplayAudio.asset`。Bootstrap・WiringLab・HeightLab・ExpansionLab・TokyoStationWiringLabと、それらのシーン生成処理へ設定を渡した。Inspectionシーンは対象外。

## 音量と同時発音（暫定値）

- Master Volumeは0.8。Waveはその0.85倍で、視点の距離によらない。
- Overview／経路編集は、画面中央から正規化した距離に応じて減衰。表示半径1.15〜2.2で画面外へフェードし、orthographicSize 48 mを基準にズームアウトで下げる。カメラ高度では減衰させないため、屋上Nodeも画面上の位置に対応する。
- 360は実距離を使い、12 mまで最大、65 mを減衰の基準、400〜600 mでフェードアウト。左右のパンは最大±0.7。
- Node用8音＋Wave専用1音。同じNodeの同フレームの出発／到達は1音にまとめ、0.08ゲーム秒の連打制限。空きがなければ聞こえやすい音を優先し、抑制分をキューに残さない。
- Node全体のゲインは `Master Volume × 0.65 / sqrt(同時発音数)`。承認音源のピークと合わせて重なりの余裕を確保する。値は最終音響調整ではなく暫定。
- 再生中もカメラ移動に追従。ゲームPause・Editor Pause・Game Overで音を停止し、再開／再有効化時は古い履歴を再生しない。

## 検証

- Red：輸送累計と距離計算のEditMode 3件、音声連携のPlayMode 4件が未実装で失敗することを確認。
- Green：関連EditMode **56/56成功**。Snapshotの不変性、実際の出発／到達の累計、既存の輸送・Routing・Pause・Waveと空間音量を確認。
- 関連PlayMode **24/24成功**。新規GameplayAudio 5件に加え、SimulationDriver・FlowSimulation・PauseInput・WaveSession・TokyoStationWiringLab・NodeArrival・NodeMinimapを実行。実Editor Pause、再開後の無再送、Game Over、上限8音と近い音の優先、シーン終了時の解放、1つのAudioListener、原音の非ゼロPCMデータを確認。ExpansionLabのPlayModeテストは作成・実行していない。
- コンパイルError／Warning **0**。
- 東京駅の実シーンで初期3本の配線を作り、Source出発→Sink到達→Wave 2を再生。AudioSource出力の絶対ピークはそれぞれ **0.03557432 / 0.05104591 / 0.3105252**、AudioListener出力は各観測区間で **0.03557432 / 0.05104591 / 0.3530423**。Unityの音声出力が非ゼロであることを確認した（スピーカーからの録音による確認ではない）。Wave 2、配送12、Game Overなし、AudioSource総数9。
- UnityのMute Audioは解除済み、AudioListener.volumeは1、pauseはfalse。検証用の配線・時計操作は保存せず、東京駅シーンを停止状態へ戻す。Playすると初期Waveが鳴り、配線後に出発・到達音が鳴る。
