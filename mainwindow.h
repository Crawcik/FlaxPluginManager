#ifndef MAINWINDOW_H
#define MAINWINDOW_H

#include <QMainWindow>
#include <QListWidget>
#include <QNetworkAccessManager>
#include <QNetworkReply>

#define JSON_URL "https://gist.githubusercontent.com/Crawcik/5178b70e52ea577cb1100dd6a4884749/raw/flax_plugins_list.json"

QT_BEGIN_NAMESPACE
namespace Ui { class MainWindow; }
QT_END_NAMESPACE

class MainWindow : public QMainWindow
{
    Q_OBJECT

public:
    MainWindow(QWidget *parent = nullptr);
    ~MainWindow();

private slots:
    void GetRequest(QNetworkReply *reply);

private:
    Ui::MainWindow *ui;
    QListWidget *ui_list;
};
#endif // MAINWINDOW_H
