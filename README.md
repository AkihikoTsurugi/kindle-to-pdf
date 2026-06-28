# Kindle to PDF

Kindle ビューアなど画面上の指定範囲を連続キャプチャし、PDF にまとめる Windows 用デスクトップアプリです。OCR（[NDLOCR-Lite](https://github.com/ndl-lab/ndlocr-lite)）にも対応しています。

## 必要環境

- Windows 10 / 11
- .NET Framework 4.x（Windows 標準搭載の `csc.exe` でビルド）
- OCR を使う場合: Python 3.12 + NDLOCR-Lite（任意）

## 初回セットアップ

1. リポジトリを clone する
2. `install_kindle_to_pdf.bat` をダブルクリック
   - `KindleToPdf.exe` をビルド
   - スタートメニュー・デスクトップに「Kindle to PDF」ショートカットを作成

## 使い方

1. Kindle（または PDF ビューア）で最初のページを表示
2. 「Kindle to PDF」を起動
3. ページ数・出力 PDF・必要なら OCR を設定
4. 「範囲を選択して開始」をクリック
5. キャプチャ範囲をドラッグ

## OCR（NDLOCR-Lite）のセットアップ

```powershell
cd %USERPROFILE%\Documents\Codex
git clone https://github.com/ndl-lab/ndlocr-lite
cd ndlocr-lite\src
pip install -r requirements.txt
```

`ndlocr-lite` は **このリポジトリと同じ親フォルダ**（例: `Documents\Codex\ndlocr-lite`）に置いてください。

OCR コマンド欄は初期値のままで、`run_ndlocr_lite_for_scanner.bat` が自動的に使われます。

## 出力

| 出力 | 場所 |
|------|------|
| PDF | 指定した出力パス |
| キャプチャ画像 | `PDF名_images_日時` フォルダ |
| OCR 結果 | `PDF名_ocr_日時` フォルダ（`merged_text.txt` が全文） |

## ファイル構成

| ファイル | 説明 |
|----------|------|
| `ScreenScanToPdf.cs` | アプリ本体ソース |
| `build_kindle_to_pdf.bat` | exe ビルド |
| `install_kindle_to_pdf.bat` | ショートカット登録 |
| `Kindle to PDF.bat` | 直接起動 |
| `run_ndlocr_lite_for_scanner.bat` | OCR ラッパー |

## ライセンス

MIT License（ソースコード部分）。キャプチャ対象のコンテンツの利用は各サービスの利用規約に従ってください。
