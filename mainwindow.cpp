#include "mainwindow.h"
#include "ui_mainwindow.h"

MainWindow::MainWindow(QWidget *parent)
    : QMainWindow(parent)
    , ui(new Ui::MainWindow)
{
    ui->setupUi(this);
    ui_list = findChild<QListWidget*>("list", Qt::FindChildrenRecursively);
    QNetworkAccessManager *manager = new QNetworkAccessManager(this);
    connect(manager, &QNetworkAccessManager::finished,
            this, &MainWindow::GetRequest);

    manager->get(QNetworkRequest(QUrl(JSON_URL)));
}

MainWindow::~MainWindow()
{
    delete ui;
}


void MainWindow::GetRequest(QNetworkReply *reply)
{
    QString replyText = reply->readAll();
    reply->deleteLater();
    QJsonDocument doc = QJsonDocument::fromJson(replyText.toUtf8());
    QJsonArray arr = doc.array();
    for (auto val : arr) {
        QJsonObject obj = val.toObject();
        QListWidgetItem *item = new QListWidgetItem(ui_list);
        item->setText(obj["name"].toString());
        item->setToolTip(obj["description"].toString());
        item->setCheckState(Qt::Unchecked);
    }
}
