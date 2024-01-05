function(task, responses){
    if(task.status.includes("error")){
        if(responses.length > 0){
            let latestResponse = responses.slice(-1);
            console.log(latestResponse);
            return {"plaintext": latestResponse.toString() };
        }
    }else if(task.completed){
        if(responses.length > 0){
            let latestResponse = responses.slice(-1);
            try{
                return {"download":[{
                    "agent_file_id": latestResponse,
                    "variant": "contained",
                    "name": "Download " + task["display_params"]
                }]};
            }catch(error){
                return {'plaintext': error};
            }

        }else{
            return {"plaintext": "No data to display..."}
        }

    }else if(task.status === "processed"){
        if(responses.length > 0){
            const task_data = responses.slice(-1);
            return {"plaintext": "Downloading file... " + task_data};
        }
        return {"plaintext": "No data yet..."}
    }else{
        // this means we shouldn't have any output
        return {"plaintext": "No response yet from agent..."}
    }
}