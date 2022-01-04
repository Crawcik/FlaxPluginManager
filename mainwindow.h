#ifndef MAINWINDOW_H
#define MAINWINDOW_H

#include <QMainWindow>
#include <QListWidget>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonArray>
#include <QFileDialog>

#define JSON_URL "https://raw.githubusercontent.com/Crawcik/FlaxPluginManager/master/plugin_list.json"

QT_BEGIN_NAMESPACE
namespace Ui { class MainWindow; }
QT_END_NAMESPACE

class MainWindow : public QMainWindow
{
    Q_OBJECT

    typedef struct _Item {
        QString name;
        QString path;
        QListWidgetItem *ui;
    } Item;

public:
    MainWindow(QWidget *parent = nullptr);
    ~MainWindow();

private slots:
    void GetRequest(QNetworkReply *reply);
    void on_select_clicked();

private:
    Ui::MainWindow *ui;
    QListWidget *ui_list;
    QString filename;
    QList<Item> *items;


};
#endif // MAINWINDOW_H
