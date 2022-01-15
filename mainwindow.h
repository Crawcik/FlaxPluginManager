#ifndef MAINWINDOW_H
#define MAINWINDOW_H

#include <QMainWindow>
#include <QProgressBar>
#include <QListWidget>
#include <QPushButton>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonArray>
#include <QFileDialog>
#include <QMessageBox>
#include <QProcess>
#include <zlib.h>


#define LIST_URL "https://raw.githubusercontent.com/Crawcik/FlaxPluginManager/master/plugin_list.json"
#define TREE_URL "https://api.github.com/repos/%1/git/trees/master?recursive=true"
#define RAW_URL  "https://raw.githubusercontent.com/%1/master/"
#define GITHUB_URL  "https://github.com/"

QT_BEGIN_NAMESPACE
namespace Ui { class MainWindow; }
QT_END_NAMESPACE

class MainWindow : public QMainWindow
{
    Q_OBJECT

    typedef struct _Item {
        QString name;
        QString path;
        QString url;
        QString moduleName;
        QListWidgetItem *ui;
    } Item;

    typedef struct _Repo {
        Item* item;
        QDir path;
        QList<QString>* files;
        int initLenght;
    } Repo;

public:
    MainWindow(QWidget *parent = nullptr);
    ~MainWindow();

private slots:
    void GetRequest(QNetworkReply *reply);
    void UpdateFiles();
    QByteArray UpdateFlaxproj(const QString &content);
    bool UpdateDependencies(const QDir &dir);
    bool TryGitDownload(const QDir &dir);
    void TryDirectDownload(QNetworkReply *reply);
    void on_select_clicked();
    void on_apply_clicked();

private:
    Ui::MainWindow *ui;
    QNetworkAccessManager *manager;
    QProgressBar *progressBar;
    QListWidget *ui_list;
    QPushButton *apply_button;
    QString filename;
    QString gameTarget;
    QMetaObject::Connection connection;
    QList<Item*> *items;
    QList<Item*> *cachedItems;
    QList<Repo> *toDownload;
    int downloadIndex = 0;
};
#endif // MAINWINDOW_H
