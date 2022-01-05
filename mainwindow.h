#ifndef MAINWINDOW_H
#define MAINWINDOW_H

#include <QMainWindow>
#include <QListWidget>
#include <QPushButton>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonArray>
#include <QFileDialog>
#include <QMessageBox>

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
        QString moduleName;
        QString moduleEditorName;
        QListWidgetItem *ui;
    } Item;

public:
    MainWindow(QWidget *parent = nullptr);
    ~MainWindow();

private slots:
    void GetRequest(QNetworkReply *reply);
    void on_select_clicked();
    void on_apply_clicked();

private:
    Ui::MainWindow *ui;
    QListWidget *ui_list;
    QPushButton *apply_button;
    QString filename;
    QList<Item*> *items;
    QList<Item*> *cachedItems;
};
#endif // MAINWINDOW_H
