# PID Arm Unity

Unity 2D で作成した、1 自由度の単振り子型ロボットアームの PID 制御シミュレーションです。

Play すると、スクリプトによってカメラ、UI、支点、アーム、HingeJoint2D、ガイド線、軌跡、角度グラフが自動生成されます。Unity エディタ上でシーンオブジェクトを手動配置する必要はありません。

## 動作確認環境

- OS: Windows 11
- Unity Editor: 6000.3.18f1
- IDE: Visual Studio Code
- Git: Git for Windows

Unity の正確なバージョンは [ProjectSettings/ProjectVersion.txt](ProjectSettings/ProjectVersion.txt) に記録されています。基本的には Unity Hub で同じ `6000.3.18f1` をインストールして開いてください。

## クローン

PowerShell または Git Bash で任意の作業フォルダに移動し、以下を実行します。

```powershell
git clone https://github.com/sousci/pid-arm-unity.git
cd pid-arm-unity
```

## Unity で開く

1. Unity Hub を起動します。
2. `Add` または `Open` から、クローンした `pid-arm-unity` フォルダを選択します。
3. Unity Editor `6000.3.18f1` で開きます。
4. 初回起動時は `Library/` が自動生成されるため、インポートに時間がかかります。
5. エディタ上部の Play ボタンを押します。

Play するとシーンが自動構築されます。`Start` ボタンで PID 制御が開始し、`Reset` ボタンで角度、角速度、積分値、軌跡、グラフが初期化されます。

## VS Code で編集する

VS Code 側は以下の拡張機能を入れておくと編集しやすくなります。

- C# Dev Kit
- C#
- Unity

Unity 側で VS Code を外部エディタに設定します。

1. Unity のメニューから `Edit > Preferences...` を開きます。
2. `External Tools` を選びます。
3. `External Script Editor` に Visual Studio Code を指定します。
4. `Regenerate project files` を実行します。

その後、Unity の Project ビューから C# スクリプトを開くか、VS Code でリポジトリフォルダを開いて編集します。

```powershell
code .
```

## 主な編集対象

- [Assets/Scripts/PIDArmSceneBuilder.cs](Assets/Scripts/PIDArmSceneBuilder.cs)
  - カメラ、Canvas、UI、ロボットアーム、支点、ガイド線、背景、角度グラフなどの自動生成を担当します。
- [Assets/Scripts/PIDArmController.cs](Assets/Scripts/PIDArmController.cs)
  - PID 制御、角度計算、トルク付加、軌跡表示、角度グラフ、収束判定、Reset 処理を担当します。

## 動作確認の流れ

1. Unity でプロジェクトを開きます。
2. Console に C# コンパイルエラーがないことを確認します。
3. Play ボタンを押します。
4. 右側 UI の `Start` を押します。
5. アームが目標角度に向かって動作することを確認します。
6. P / I / D ゲイン、目標角度、アーム長、質量、最大トルク、減衰係数を変更し、挙動と左下グラフがリアルタイムに変化することを確認します。
7. 収束状態、Start から収束までの時間、オーバーシュートの有無と角度が表示されることを確認します。
8. `Reset` を押し、アーム、軌跡、角度グラフが初期化されることを確認します。

## 練習問題

シミュレーションを実行し、Start と Reset を使いながら条件をそろえて確認してください。収束時間は画面右側の収束状態表示、オーバーシュートは Overshoot 表示と左下グラフで確認します。

1. P 制御のみで、目標角度 30 deg、アーム長 2 m、質量 1 kg、減衰係数 0.3 のとき、システムは収束しますか。P ゲインを 5、20、50、100 に変えて、収束時間とオーバーシュートの有無を記録してください。
2. P 制御のみで収束しにくい、または振動が残る条件を 1 つ探してください。見つけた条件について、なぜ P 項だけでは十分でないのかを偏差、重力、減衰の観点から説明してください。
3. PD 制御で、目標角度 30 deg、アーム長 2 m、質量 1 kg、減衰係数 0.3 のとき、オーバーシュートなしで 3 秒以内に収束する P / D ゲインの組み合わせを 1 つ答えてください。
4. PI 制御で、目標角度 60 deg、アーム長 2 m、質量 1 kg、減衰係数 0.3 のとき、I ゲインを上げると応答はどう変わりますか。I ゲイン 0、1、5、10 で比較し、収束時間とオーバーシュートを記録してください。
5. PID 制御で、目標角度 90 deg、アーム長 2 m、質量 1 kg、減衰係数 0.3 のとき、オーバーシュートありで 2 秒以内に収束する P / I / D ゲインの組み合わせを 1 つ答えてください。
6. PID 制御で、目標角度 90 deg、アーム長 2 m、質量 1 kg、減衰係数 0.3 のとき、オーバーシュートなしで 4 秒以内に収束する P / I / D ゲインの組み合わせを 1 つ答えてください。
7. 限界感度法を参考に、アーム長 4 m、質量 5 kg、目標角度 90 deg、減衰係数 0.3 の条件で、持続振動に近い P ゲインを探してください。その値をもとに、オーバーシュートありで 5 秒以内に収束する P / I / D ゲインの組み合わせを 1 つ答えてください。
8. 問題 7 と同じアーム長 4 m、質量 5 kg、目標角度 90 deg の条件で、オーバーシュートなしで 8 秒以内に収束する P / I / D ゲイン、最大トルク、減衰係数の組み合わせを 1 つ答えてください。問題 7 の結果と比べて、D ゲインと減衰係数が応答に与えた影響を説明してください。

## Git で編集内容を保存する

変更内容を確認します。

```powershell
git status
git diff
```

コミットしてプッシュします。

```powershell
git add .
git commit -m "Describe your change"
git push
```

## Git に含めないもの

以下は Unity が自動生成するため、`.gitignore` で除外しています。

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`
- `Build/`
- `Builds/`

他の人がクローンした場合も、Unity で開くと必要な生成物は自動で作られます。
