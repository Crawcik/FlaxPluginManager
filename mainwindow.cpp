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
    QListWidgetItem *item = new QListWidgetItem(ui_list);
    item->setText("Bruh");
    item->setCheckState(Qt::Unchecked);
}
