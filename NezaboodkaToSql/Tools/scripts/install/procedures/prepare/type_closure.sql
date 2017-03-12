CREATE TABLE `${db_name}`.`type_closure`(
    `ancestor` INT NOT NULL,
    `descendant` INT NOT NULL,
    
    FOREIGN KEY(`ancestor`)
        REFERENCES `type`(`id`)
        ON DELETE CASCADE,
    
    FOREIGN KEY(`descendant`)
        REFERENCES `type`(`id`)
        ON DELETE CASCADE,
    
    CONSTRAINT `uc_keys`
        UNIQUE (`ancestor`, `descendant`)
) ENGINE=`INNODB`;
