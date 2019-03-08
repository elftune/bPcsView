# bPcsView
簡易BMS再生ツール

## Overview
- BMSファイルの簡易再生アプリ（ビューワーであり、ゲーム機能は無い）
![Image](https://elftune.github.io/app/20190307_1.png)  
BMSはJunkさんのLife is PIANO[Rea'ju]

## Requirements
- Microsoft Visual Studio 2017 Community
- Microsoft Windows 10 64bit (MUST)
- .NET Framework 4.7.1
- DirectX 11 + High Performance PC
- RAM 16GB+
- BMS File(s)

## Notice
- 対応しているBMSは「作者の手元にあるBMSファイルについて再生できる程度」の対応状況なので注意
- BGAはH.264/AVC MP4、OGV、OGX形式を推奨。H.265/HEVC MP4は再生できないので注意。bPcsのスピンオフなのでMPEG1、WMV、AVIなどでは一切検証していない
- BMSの中には、破損している/読み込めないOGGファイルが存在するものがある。非常に長い時間読み込みリトライする場合があるので注意。bPcsでは一度正常に
読めなかったら拒否リストに登録して2回目以降は読まないようにしているが、このアプリにはその機能が無いので注意
- 64bitアプリで64bit版MP3コーデックを使用すると、読み込みに非常に時間がかかるものがある。あらかじめWAVに変換しておいた方がよいかもしれない

## Usage
- bPcsView.sln をVisual Studioでビルドし、実行する
- BMSファイルをドラッグ＆ドロップするとLOAD後、再生する
- Listでダイアログを開き、フォルダやBMSファイルをDnDし、ランダム再生できる
- CodePageを指定できる (Listでは効かないので注意)
- Make RND Expand Fileにチェックを入れると、random命令のあるBMSは展開結果ファイルをデスクトップに保存する
- ReLoadをBMSの再生中にクリックすると、WAV・静止画BGIはそのままで、BMSと動画BGAのみ再読み込みして再生を開始する。BMSの作成などで便利かもしれない
- MRUは30曲くらいまで保持。MRUはMRUList.txtファイルに残る。Edit MRU File/ReLoadから直接編集/読み込みできる

## Special Thanks
- 本アプリは ＤＸライブラリ を使用しています。ライセンス等はDxLib.txtを参照のこと。
