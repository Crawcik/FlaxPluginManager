#include "mainwindow.h"
#include "ui_mainwindow.h"

MainWindow::MainWindow(QWidget *parent)
    : QMainWindow(parent)
    , ui(new Ui::MainWindow)
{
    ui->setupUi(this);
    ui_list = findChild<QListWidget*>("list", Qt::FindChildrenRecursively);
    apply_button = findChild<QPushButton*>("apply", Qt::FindChildrenRecursively);
    ui_list->setEnabled(false);
    apply_button->setEnabled(false);

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
    items = new QList<Item*>();
    cachedItems = new QList<Item*>();
    for (auto val : arr)
    {
        QJsonObject obj = val.toObject();
        Item *item = new Item();
        item->name = obj["name"].toString(),
        item->path = obj["projectFile"].toString(),
        item->moduleName = obj["moduleName"].toString(),
        item->moduleEditorName = obj["moduleEditorName"].toString(),
        item->ui = new QListWidgetItem(ui_list);
        item->ui->setText(item->name);
        item->ui->setToolTip(obj["description"].toString());
        item->ui->setCheckState(Qt::Unchecked);
        items->append(item);
    }
}

void MainWindow::on_select_clicked()
{
    apply_button->setEnabled(false);
    filename = QFileDialog::getOpenFileName(this,
        tr("Open Flax project"), nullptr, tr("Flaxproj (*.flaxproj)"));
    QFile file(filename);
    if (!file.open(QIODevice::ReadOnly | QIODevice::Text))
    {
        QMessageBox::warning(this, "Error", "Cannot read this file!");
        return;
    }
    setWindowTitle(file.fileName());
    ui_list->setEnabled(true);
    QString content = file.readAll();
    file.close();
    QJsonDocument doc = QJsonDocument::fromJson(content.toUtf8());
    QJsonArray arr = doc["References"].toArray();
    for (auto val : arr)
    {
        QString path = val.toObject()["Name"].toString();
        for(int i = 0; i < items->count(); i++)
        {
            Item* item = items->at(i);
            if(path.contains(item->path))
            {
                item->ui->setCheckState(Qt::Checked);
                cachedItems->append(item);
            }

        }
    }
    apply_button->setEnabled(true);
}

void MainWindow::on_apply_clicked()
{
    QFile file(filename);
    QDir directory = QFileInfo(filename).absoluteDir();
    if (!file.open(QIODevice::ReadWrite | QIODevice::Text))
    {
        QMessageBox::warning(this, "Error", "Cannot read/write in flaxproj file!");
        return;
    }
    if(!directory.exists("Plugins"))
    {
        directory.mkdir("Plugins");
    }
    ui_list->setEnabled(true);
    QString content = file.readAll();
    QJsonDocument doc = QJsonDocument::fromJson(content.toUtf8());
    QJsonObject root = doc.object();
    QJsonArray arr = root["References"].toArray();
    for(int i = 0; i < items->count(); i++)
    {
        Item* item = items->at(i);
        bool checked = item->ui->checkState() == Qt::Checked;
        // XNOR gate. cached contains means its already installed
        if(cachedItems->contains(item) == checked)
            continue;
        if(checked)
        {
            QJsonObject obj;
            obj.insert("Name", QJsonValue("$(ProjectPath)/Plugins/" + item->path));
            cachedItems->append(item);
            arr.append(obj);
        }
        else
        {
            for(int j = 0; j < arr.count(); j++)
            {
                QJsonObject arrObj = arr[j].toObject();
                if(arrObj["Name"].toString().contains(item->path))
                {
                    arr.removeAt(j);
                    cachedItems->removeOne(item);
                    break;
                }
            }
        }
    }
    root["References"] = arr;
    doc.setObject(root);
    file.resize(0);
    file.seek(0);
    file.write(doc.toJson(QJsonDocument::JsonFormat::Indented));
    file.close();
    apply_button->setEnabled(true);
}

