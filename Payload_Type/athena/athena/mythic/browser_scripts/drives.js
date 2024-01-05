function(task, responses){
    if(task.status.includes("error")){
        const combined = responses.reduce( (prev, cur) => {
            return prev + cur;
        }, "");
        return {'plaintext': combined};
    }else if(task.completed){
        if(responses.length > 0){
            try{
                    let data = JSON.parse(responses[0]);
                    let output_table = [];
                    for(let i = 0; i < data.length; i++){
                        output_table.push({
                            "DriveName":{"plaintext": data[i].DriveName},
                            "DriveType": {"plaintext": data[i].DriveType},
                            "FreeSpace": {"plaintext": data[i].FreeSpace},
                            "TotalSpace": {"plaintext": data[i].TotalSpace}
                        })
                    }
                    return {
                        "table": [
                            {
                                "headers": [
                                    {"plaintext": "DriveName", "type": "string"},
                                    {"plaintext": "DriveType", "type": "number"},
                                    {"plaintext": "FreeSpace", "type": "number"},
                                    {"plaintext": "TotalSpace", "type": "number"},
                                ],
                                "rows": output_table,
                                "title": "Drives Data"
                            }
                        ]
                    }
            }catch(error){
                    console.log(error);
                    const combined = responses.reduce( (prev, cur) => {
                        return prev + cur;
                    }, "");
                    return {'plaintext': combined};
            }
        }else{
            return {"plaintext": "No output from command"};
        }
    }else{
        return {"plaintext": "No data to display..."};
    }
}