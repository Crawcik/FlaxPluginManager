#include "mainwindow.h"
#include "ui_mainwindow.h"

MainWindow::MainWindow(QWidget *parent)
    : QMainWindow(parent)
    , ui(new Ui::MainWindow)
{
    ui->setupUi(this);
    ui_list = findChild<QListWidget*>("list", Qt::FindChildrenRecursively);
    ui_list->setEnabled(false);

    QNetworkAccessManager *manager = new QNetworkAccessManager(this);
    connect(manager, &QNetworkAccessManager::finished, this, &MainWindow::GetRequest);
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
    items = new QList<Item>();
    for (auto val : arr)
    {
        QJsonObject obj = val.toObject();
        Item item = {
            .name = obj["name"].toString(),
            .path = obj["projectFile"].toString(),
            .ui = new QListWidgetItem(ui_list)
        };
        item.ui->setText(item.name);
        item.ui->setToolTip(obj["description"].toString());
        item.ui->setCheckState(Qt::Unchecked);
        items->append(item);
    }
}

void MainWindow::on_select_clicked()
{
    filename = QFileDialog::getOpenFileName(this,
        tr("Open Flax project"), nullptr, tr("Flaxproj (*.flaxproj)"));
    QFile file(filename);
    if (!file.open(QIODevice::ReadOnly | QIODevice::Text))
            return;
    setWindowTitle(file.fileName());
    ui_list->setEnabled(true);
    QString content = file.readAll();
    QJsonDocument doc = QJsonDocument::fromJson(content.toUtf8());
    QJsonArray arr = doc["References"].toArray();
    for (auto val : arr)
    {
        QString path = val.toObject()["Name"].toString();
        if(path.contains("$(ProjectPath)/Plugins/"))
        {
            path = path.remove(0, sizeof("$(ProjectPath)/Plugins/"));
        }
        for (int i = 0; i < items->count(); i++)
        {
            Item item = items->at(i);
            if(path.contains(item.path))
            {
                item.ui->setCheckState(Qt::Checked);
            }

        }
    }
}
