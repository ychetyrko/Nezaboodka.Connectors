CREATE TABLE `${db_name}`.`list_item` (
    `id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
    `list_id` INT NOT NULL,
    `ref` BIGINT(0) NOT NULL,
    
    FOREIGN KEY (`list_id`)
        REFERENCES `list`(`id`)
        ON DELETE CASCADE,	-- clear removed lists
    
    FOREIGN KEY (`ref`)
        REFERENCES `db_key`(`sys_id`)
        ON DELETE CASCADE,	-- remove deleted object from all lists

    CONSTRAINT `uc_list_ref`	-- no duplicates
        UNIQUE (`list_id`, `ref`)
) ENGINE=`INNODB`;