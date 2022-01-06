#include "mainwindow.h"
#include "ui_mainwindow.h"

MainWindow::MainWindow(QWidget *parent)
    : QMainWindow(parent)
    , ui(new Ui::MainWindow)
{
    ui->setupUi(this);
    ui_list = findChild<QListWidget*>("list", Qt::FindChildrenRecursively);
    progressBar = findChild<QProgressBar*>("progressBar", Qt::FindChildrenRecursively);
    apply_button = findChild<QPushButton*>("apply", Qt::FindChildrenRecursively);
    ui_list->setEnabled(false);
    progressBar->hide();
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
        item->url = obj["url"].toString(),
        item->path = obj["projectFile"].toString(),
        item->moduleName = obj["moduleName"].toString(),
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
    gameTarget = doc["GameTarget"].toString().remove("Target");
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
    apply_button->setEnabled(false);
    QFile file(filename);
    QDir directory = QFileInfo(filename).absoluteDir();
    if(!directory.exists("Plugins"))
    {
        directory.mkdir("Plugins");
    }

    // Download plugins
    QDir pluginDir = directory;
    pluginDir.cd("Plugins");
    progressBar->setValue(0);
    progressBar->show();
    if(!TryGitDownload(pluginDir))
    {
        if(!TryZipDownload(pluginDir))
        {
            QMessageBox::warning(this, "Error", "Downloading plugins failed!");
            return;
        }
    }
    progressBar->hide();
    // -----------
    // Update flaxproj
    if (!file.open(QIODevice::ReadWrite | QIODevice::Text))
    {
        QMessageBox::warning(this, "Error", "Cannot read/write in flaxproj file!");
        return;
    }
    QString content = file.readAll();
    file.resize(0);
    file.seek(0);
    file.write(UpdateFlaxproj(content));
    file.close();
    // -----------
    // Update modules
    if(!UpdateDependencies(directory))
    {
        QMessageBox::warning(this, "Warning", "Adding code dependencies failed. But plugins were installed. Try add manually");
    }
    // -----------
    apply_button->setEnabled(true);
}

QByteArray MainWindow::UpdateFlaxproj(const QString &content)
{
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
            obj.insert("Name", QJsonValue("$(ProjectPath)/Plugins/" + item->name  + '/' + item->path));
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
    return doc.toJson(QJsonDocument::JsonFormat::Indented);
}

// Abomination
bool MainWindow::UpdateDependencies(const QDir &dir)
{
    const QString pattern("        options.PrivateDependencies.Add(\"%1\");"); // Discusting spaces...
    QString filePath, line;
    bool targetNotFound = true;
    if(!gameTarget.isEmpty())
    {
        filePath = dir.absoluteFilePath(QString("Source/%1/%1.Build.cs").arg(gameTarget));
        if(QFile::exists(filePath))
            targetNotFound = false;
    }
    if(targetNotFound)
    {
        filePath = dir.absoluteFilePath("Source/Game/Game.Build.cs");
        if(!QFile::exists(filePath))
            return false;
    }

    QFile file(filePath);
    if (!file.open(QIODevice::ReadWrite | QIODevice::Text))
        return false;
    QTextStream stream(&file);
    QList<Item*> itemsToImp = *cachedItems;

    // Here its going to be more busted but im tired
    int lineNum = 0, startNum = 0, insertion = 0;
    bool searchingSeek = true;
    while (stream.readLineInto(&line)) {
        if(startNum != 0)
        {   
            // Finding end to speed up process (or slow it XD)
            if(line.contains('{'))
                insertion++;
            if(line.contains('}'))
            {
                insertion--;
                if(insertion == 0)
                   break;
            }
        }

        // Finding function
        if(line.contains("public override void Setup(BuildOptions options)"))
        {
            insertion = line.contains('{') ? 0 : 1;
            startNum = lineNum + insertion;
            insertion = 1 - insertion;
        }

        // Getting pos to seek
        if(insertion == 1 && searchingSeek == true)
        {
            startNum = stream.pos();
            searchingSeek = false;
        }
        lineNum++;
    }
    if(startNum == 0)
        return false;

    line = "";
    stream.seek(startNum);
    while(!stream.atEnd())
    {
        QString tmpLine = stream.readLine();
        bool skip = false;
        for(int i = 0; i < items->count(); i++)
        {
            Item* item = items->at(i);
            if(item->moduleName.isEmpty())
                continue;
            if(!tmpLine.contains('"' + item->moduleName + '"'))
                continue;
            skip = true;
            break;
        }
        if(skip)
            continue;
        line.append(tmpLine);
        line.append('\n');
    }
    stream.seek(startNum);
    for(int i = 0; i < itemsToImp.count(); i++)
    {
        Item* item = itemsToImp.at(i);
        if(!item->moduleName.isEmpty())
            stream << pattern.arg(item->moduleName) << Qt::endl;
    }
    stream << line;
    file.resize(stream.pos());
    file.close();
    return true;
}

bool MainWindow::TryGitDownload(const QDir &dir)
{
    // Detect git
    bool moduleCrached = false, submodule = false;
    QProcess process;
    process.setWorkingDirectory(dir.absolutePath());
    process.start("git", QStringList() << "--version");
    if(!process.waitForFinished(1000))
        return false;
    if(process.exitCode() != 0)
        return false;

    // Checking on submodules
    process.start("git", QStringList() << "status");
    if(!process.waitForFinished(1000))
        return false;
    submodule = process.exitCode() == 0;
    // Counting checked!
    int count = 0, done = 0;
    for(int i = 0; i < items->count(); i++)
    {
        Item* item = items->at(i);
        if((item->ui->checkState() != Qt::Checked) == dir.exists(item->name))
            count++;
    }
    if(count == 0)
        return true;

    for(int i = 0; i < items->count(); i++)
    {
        progressBar->setValue((done * 100) / count);
        Item* item = items->at(i);
        if(item->ui->checkState() != Qt::Checked && dir.exists(item->name))
        {
            // Deleting plugin
            done++;
            if(submodule)
            {
                QFile gitFile(dir.filePath("../.gitmodules"));
                if(!gitFile.exists() || !gitFile.open(QIODevice::ReadWrite  | QIODevice::Text))
                    continue;
                QTextStream stream(&gitFile);
                QString newFileContent;
                process.start("git", QStringList() << "submodule" << "deinit" << "-f" << item->name);
                process.waitForFinished();
                while(!stream.atEnd())
                {
                    QString tmpLine = stream.readLine();
                    if(tmpLine.startsWith('[') && tmpLine.contains(item->name))
                    {
                        stream.readLine();
                        stream.readLine();
                        continue;
                    }
                    newFileContent.append(tmpLine);
                    newFileContent.append('\n');
                }
                stream.seek(0);
                stream << newFileContent;
                gitFile.resize(stream.pos());
            }
            dir.rmdir(item->name);
            continue;
        }
        // Adding plugin
        if(item->ui->checkState() != Qt::Checked)
            continue;
        done++;
        QStringList args;
        args << (submodule ? "submodule" : "clone");
        if(submodule)
            args << "add" << "--force";
        args << item->url << item->name;
        process.start("git", args);
        if(process.waitForFinished() && process.exitCode() == 0)
            continue;
        moduleCrached = true;
        item->ui->setCheckState(Qt::Unchecked);
    }
    if(submodule)
    {
        process.start("git", QStringList() << "submodule" << "update" << "--recursice");
        process.waitForFinished();
    }
    if(moduleCrached)
    {
        QMessageBox::warning(this, "Warning", "Some plugins couldn't be downloaded!");
    }
    return true;
}

bool MainWindow::TryZipDownload(const QDir &dir)
{
return false;
}
